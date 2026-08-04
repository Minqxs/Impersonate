using System.Text;
using System.Text.Json;
using Impersonate.Application.Delivery;
using Impersonate.Application.Projects;
using Impersonate.Domain.Delivery;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery.Mcp;

internal sealed class GitHubMcpPullRequestGateway(IProjectRepository projects, IRunDeliveryRepository runDeliveries, IGitHubMcpClient mcp, IOptions<GitHubMcpOptions> configured) : IPullRequestGateway
{
    private readonly GitHubMcpOptions options = configured.Value;

    public async Task<DeliveryOperationResult<PullRequestReference>> OpenAsync(TaskDelivery delivery, ApprovedTaskHandoff handoff, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mcp.ServerIdentity) || mcp.ServerIdentity.Length > 35 || mcp.ServerIdentity.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('-' or '_')))
            return Fail("github_mcp_server_not_allowed", "GitHub MCP server identity is invalid.");
        if (delivery.Status != TaskDeliveryStatus.Pushed || string.IsNullOrWhiteSpace(delivery.RemoteRepository) || string.IsNullOrWhiteSpace(delivery.RemoteBranchName) || string.IsNullOrWhiteSpace(delivery.PushedCommitSha))
            return Fail("delivery_pull_request_state_invalid", "Only a verified pushed delivery can open a pull request.");
        if (!string.Equals(delivery.PushedCommitSha, delivery.CommitSha, StringComparison.OrdinalIgnoreCase))
            return Fail("delivery_pull_request_head_changed", "Pushed commit no longer matches the approved delivery commit.");
        var project = await projects.GetAsync(delivery.ProjectId, ct);
        if (project is null)
            return Fail("delivery_project_not_found", "Delivery project was not found.");
        var runDelivery = await runDeliveries.GetByRunAsync(delivery.ProjectId, delivery.PipelineRunId, ct);
        if (runDelivery is null || runDelivery.Status != RunDeliveryStatus.IntegratingTasks)
            return Fail("run_delivery_branch_not_ready", "Run integration branch is not ready for internal task pull requests.");
        var baseBranch = runDelivery.RunBranchName;
        var repository = RepositoryIdentity(project.RepositoryUrl);
        if (repository is null || !string.Equals(repository, delivery.RemoteRepository, StringComparison.OrdinalIgnoreCase) || !options.AllowedRepositories.Contains(repository, StringComparer.OrdinalIgnoreCase))
            return Fail("github_mcp_repository_not_allowed", "Repository is not allowed for GitHub MCP delivery.");
        var parts = repository.Split('/');
        try
        {
            var existing = await FindAsync(parts[0], parts[1], baseBranch, delivery.RemoteBranchName, ct);
            if (existing is not null)
                return Match(existing, delivery, repository, baseBranch);
            JsonElement created;
            try
            {
                created = await mcp.CallToolAsync("create_pull_request", new
                {
                    owner = parts[0],
                    repo = parts[1],
                    title = handoff.Title,
                    head = delivery.RemoteBranchName,
                    @base = baseBranch,
                    body = Body(delivery, handoff, baseBranch),
                    draft = options.DraftPullRequests,
                    maintainer_can_modify = true
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                existing = await FindAsync(parts[0], parts[1], baseBranch, delivery.RemoteBranchName, ct);
                return existing is null ? Fail("github_mcp_create_failed", "GitHub MCP pull-request creation failed safely.") : Match(existing, delivery, repository, baseBranch);
            }
            var number = Long(created, "number") ?? Long(created, "pull_number") ?? throw new InvalidOperationException("github_mcp_malformed_response");
            var exact = await mcp.CallToolAsync("pull_request_read", new
            {
                method = "get",
                owner = parts[0],
                repo = parts[1],
                pullNumber = number
            }, ct);
            return Match(Parse(exact), delivery, repository, baseBranch);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var code = ex.Message.StartsWith("github_mcp_", StringComparison.Ordinal) ? ex.Message : "github_mcp_failed";
            return Fail(code, "GitHub MCP pull-request operation failed safely.");
        }
    }

    public async Task<DeliveryOperationResult<PullRequestObservation>> ReadAsync(TaskDelivery delivery, CancellationToken ct)
    {
        if (!string.Equals(delivery.PullRequestProvider, $"GitHubMCP:{mcp.ServerIdentity}", StringComparison.Ordinal))
            return DeliveryOperationResult<PullRequestObservation>.Fail("github_mcp_server_not_allowed", "Recorded pull request belongs to a different MCP server.");
        if (delivery.Status is not (TaskDeliveryStatus.PullRequestOpen or TaskDeliveryStatus.DeliveryReview or TaskDeliveryStatus.ApprovedForIntegration or TaskDeliveryStatus.MergeRequested) || delivery.PullRequestNumber is null || string.IsNullOrWhiteSpace(delivery.PullRequestRepository))
            return DeliveryOperationResult<PullRequestObservation>.Fail("delivery_reconciliation_state_invalid", "An open pull-request identity is required for reconciliation.");
        var parts = delivery.PullRequestRepository.Split('/');
        if (parts.Length != 2 || !options.AllowedRepositories.Contains(delivery.PullRequestRepository, StringComparer.OrdinalIgnoreCase))
            return DeliveryOperationResult<PullRequestObservation>.Fail("github_mcp_repository_not_allowed", "Repository is not allowed for GitHub MCP reconciliation.");
        try
        {
            var value = await mcp.CallToolAsync("pull_request_read", new
            {
                method = "get",
                owner = parts[0],
                repo = parts[1],
                pullNumber = delivery.PullRequestNumber.Value
            }, ct);
            var pull = Parse(value);
            if (pull.Number != delivery.PullRequestNumber || !string.Equals(pull.HeadBranch, delivery.PullRequestHeadBranch, StringComparison.Ordinal) || !string.Equals(pull.BaseBranch, delivery.PullRequestBaseBranch, StringComparison.Ordinal))
                return DeliveryOperationResult<PullRequestObservation>.Fail("delivery_pull_request_identity_changed", "Pull-request identity no longer matches the recorded delivery.");
            if (!string.Equals(pull.HeadSha, delivery.CommitSha, StringComparison.OrdinalIgnoreCase))
                return DeliveryOperationResult<PullRequestObservation>.Fail("delivery_pull_request_head_changed", "Pull-request head does not match the approved delivery commit.");
            var state = pull.Merged ? PullRequestExternalState.Merged
                : string.Equals(pull.State, "closed", StringComparison.OrdinalIgnoreCase) ? PullRequestExternalState.Closed
                : string.Equals(pull.State, "open", StringComparison.OrdinalIgnoreCase) ? PullRequestExternalState.Open
                : throw new InvalidOperationException("github_mcp_malformed_response");
            return DeliveryOperationResult<PullRequestObservation>.Ok(new($"GitHubMCP:{mcp.ServerIdentity}", delivery.PullRequestRepository, pull.Number, pull.HeadBranch, pull.BaseBranch, pull.HeadSha, state, pull.MergeCommitSha));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var code = ex.Message.StartsWith("github_mcp_", StringComparison.Ordinal) ? ex.Message : "github_mcp_failed";
            return DeliveryOperationResult<PullRequestObservation>.Fail(code, "GitHub MCP pull-request reconciliation failed safely.");
        }
    }

    private async Task<PullRequest?> FindAsync(string owner, string repo, string baseBranch, string headBranch, CancellationToken ct)
    {
        var result = await mcp.CallToolAsync("list_pull_requests", new
        {
            owner,
            repo,
            state = "all",
            head = $"{owner}:{headBranch}",
            @base = baseBranch,
            perPage = 20
        }, ct);
        return Items(result).Select(Parse).OrderByDescending(x => x.Number).FirstOrDefault(x => string.Equals(x.HeadBranch, headBranch, StringComparison.Ordinal) && string.Equals(x.BaseBranch, baseBranch, StringComparison.Ordinal));
    }
    private DeliveryOperationResult<PullRequestReference> Match(PullRequest pull, TaskDelivery delivery, string repository, string baseBranch)
    {
        if (!string.Equals(pull.HeadSha, delivery.CommitSha, StringComparison.OrdinalIgnoreCase))
            return Fail("delivery_pull_request_head_changed", "Pull-request head does not match the approved delivery commit.");
        if (!string.Equals(pull.BaseBranch, baseBranch, StringComparison.Ordinal))
            return Fail("delivery_pull_request_base_changed", "Pull-request base does not match the configured default branch.");
        if (pull.Merged || string.Equals(pull.State, "closed", StringComparison.OrdinalIgnoreCase))
            return Fail("delivery_pull_request_closed", "A matching pull request is already closed.");
        if (!Uri.TryCreate(pull.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || !string.Equals(url.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return Fail("github_mcp_malformed_response", "GitHub MCP returned an unsafe pull-request URL.");
        return DeliveryOperationResult<PullRequestReference>.Ok(new($"GitHubMCP:{mcp.ServerIdentity}", repository, pull.Number, pull.Url, pull.HeadBranch, pull.BaseBranch, pull.HeadSha, pull.CreatedAt));
    }
    private static string Body(TaskDelivery delivery, ApprovedTaskHandoff handoff, string runBranch)
    {
        var text = new StringBuilder();
        text.AppendLine($"Pipeline run: `{handoff.PipelineRunId}`").AppendLine($"Run branch: `{runBranch}`").AppendLine($"Task: {handoff.TaskSequence} — {handoff.Title}").AppendLine().AppendLine(handoff.Description).AppendLine().AppendLine("Acceptance criteria:");
        foreach (var item in handoff.AcceptanceCriteria)
            text.AppendLine($"- {item}");
        text.AppendLine().AppendLine("Changed files:");
        foreach (var file in handoff.ChangedFiles)
            text.AppendLine($"- `{file}`");
        text.AppendLine().AppendLine($"Validation: {delivery.ValidationSummaryJson}").AppendLine($"Coder: {handoff.CoderProvider} / {handoff.CoderModel}").AppendLine($"Reviewer: {handoff.ReviewerProvider} / {handoff.ReviewerModel}").AppendLine($"Review: {handoff.ReviewSummary}").AppendLine($"Patch SHA: `{handoff.ApprovedPatchSha256}`").AppendLine($"Commit SHA: `{delivery.CommitSha}`");
        if (handoff.DependencyTaskIds.Count > 0)
            text.AppendLine($"Dependencies: {string.Join(", ", handoff.DependencyTaskIds.Select(x => $"`{x}`"))}");
        return text.ToString();
    }
    private static IEnumerable<JsonElement> Items(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Select(x => x.Clone()).ToArray();
        foreach (var name in new[] { "pull_requests", "pullRequests", "items", "result" })
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array)
                return items.EnumerateArray().Select(x => x.Clone()).ToArray();
        if (value.ValueKind == JsonValueKind.Object)
            throw new InvalidOperationException("github_mcp_malformed_response");
        return [];
    }
    private static PullRequest Parse(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("pullRequest", out var nested))
            value = nested;
        var number = Long(value, "number") ?? throw new InvalidOperationException("github_mcp_malformed_response");
        var head = value.TryGetProperty("head", out var h) ? h : default;
        var @base = value.TryGetProperty("base", out var b) ? b : default;
        if (!DateTimeOffset.TryParse(Text(value, "created_at") ?? Text(value, "createdAt"), out var created))
            throw new InvalidOperationException("github_mcp_malformed_response");
        return new(number, Text(value, "html_url") ?? Text(value, "htmlUrl") ?? Text(value, "url") ?? "", Text(value, "state") ?? "", value.TryGetProperty("merged", out var merged) && merged.ValueKind == JsonValueKind.True, Text(head, "ref") ?? Text(value, "head_branch") ?? "", Text(@base, "ref") ?? Text(value, "base_branch") ?? "", Text(head, "sha") ?? Text(value, "head_sha") ?? "", created, Text(value, "merge_commit_sha") ?? Text(value, "mergeCommitSha"));
    }
    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    private static long? Long(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var item) && item.TryGetInt64(out var result) ? result : null;
    private static string? RepositoryIdentity(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return null;
        var p = uri.AbsolutePath.Trim('/').Split('/');
        if (p.Length != 2)
            return null;
        return $"{p[0]}/{(p[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? p[1][..^4] : p[1])}";
    }
    private static DeliveryOperationResult<PullRequestReference> Fail(string code, string error) => DeliveryOperationResult<PullRequestReference>.Fail(code, error);
    private sealed record PullRequest(long Number, string Url, string State, bool Merged, string HeadBranch, string BaseBranch, string HeadSha, DateTimeOffset CreatedAt, string? MergeCommitSha);
}
