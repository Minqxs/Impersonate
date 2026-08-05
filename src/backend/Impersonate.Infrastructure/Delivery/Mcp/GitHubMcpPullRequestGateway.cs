using System.Text;
using System.Text.Json;
using Impersonate.Application.Delivery;
using Impersonate.Application.Projects;
using Impersonate.Domain.Delivery;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery.Mcp;

internal sealed class GitHubMcpPullRequestGateway(IProjectRepository projects, IRunDeliveryRepository runDeliveries, IGitHubMcpClient mcp, IOptions<GitHubMcpOptions> configured) : IPullRequestGateway, IFinalPullRequestGateway
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
            try
            {
                var created = await mcp.CallToolAsync("create_pull_request", new
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
            catch
            {
                existing = await FindAsync(parts[0], parts[1], baseBranch, delivery.RemoteBranchName, ct);
                return existing is null ? Fail("github_mcp_create_failed", "GitHub MCP pull-request creation failed safely.") : Match(existing, delivery, repository, baseBranch);
            }
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
            return DeliveryOperationResult<PullRequestObservation>.Ok(new($"GitHubMCP:{mcp.ServerIdentity}", delivery.PullRequestRepository, pull.Number, pull.HeadBranch, pull.BaseBranch, pull.HeadSha, state, pull.MergeCommitSha, pull.HasConflicts));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var code = ex.Message.StartsWith("github_mcp_", StringComparison.Ordinal) ? ex.Message : "github_mcp_failed";
            return DeliveryOperationResult<PullRequestObservation>.Fail(code, "GitHub MCP pull-request reconciliation failed safely.");
        }
    }

    public async Task<DeliveryOperationResult<PullRequestReviewContext>> ReadReviewContextAsync(TaskDelivery delivery, CancellationToken ct)
    {
        var observed = await ReadAsync(delivery, ct);
        if (!observed.Succeeded)
            return DeliveryOperationResult<PullRequestReviewContext>.Fail(observed.Code!, observed.Error!);
        if (observed.Value!.State != PullRequestExternalState.Open)
            return DeliveryOperationResult<PullRequestReviewContext>.Fail("delivery_pull_request_not_open", "Only an open task pull request can be reviewed.");
        var parts = delivery.PullRequestRepository!.Split('/');
        try
        {
            var exact = await mcp.CallToolAsync("pull_request_read", new
            {
                method = "get",
                owner = parts[0],
                repo = parts[1],
                pullNumber = delivery.PullRequestNumber!.Value
            }, ct);
            var pull = Parse(exact);
            var value = await mcp.CallToolAsync("pull_request_read", new
            {
                method = "get_diff",
                owner = parts[0],
                repo = parts[1],
                pullNumber = delivery.PullRequestNumber.Value
            }, ct);
            var diff = DiffText(value);
            if (string.IsNullOrWhiteSpace(diff))
                return DeliveryOperationResult<PullRequestReviewContext>.Fail("delivery_pull_request_diff_empty", "The task pull request has no reviewable diff.");
            var files = diff.Split('\n').Where(x => x.StartsWith("+++ b/", StringComparison.Ordinal)).Select(x => x[6..].Trim()).Distinct(StringComparer.Ordinal).ToArray();
            return DeliveryOperationResult<PullRequestReviewContext>.Ok(new(pull.HeadSha, pull.BaseSha, diff, files));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return DeliveryOperationResult<PullRequestReviewContext>.Fail("github_mcp_failed", "GitHub MCP pull-request review context failed safely."); }
    }

    public async Task<DeliveryOperationResult<PullRequestObservation>> MergeAsync(TaskDelivery delivery, CancellationToken ct)
    {
        if (delivery.Status != TaskDeliveryStatus.MergeRequested || delivery.PullRequestNumber is null || string.IsNullOrWhiteSpace(delivery.PullRequestRepository) || string.IsNullOrWhiteSpace(delivery.CommitSha))
            return DeliveryOperationResult<PullRequestObservation>.Fail("delivery_merge_state_invalid", "Only an exact-head approved task pull request can be merged.");
        var parts = delivery.PullRequestRepository.Split('/');
        if (parts.Length != 2 || !options.AllowedRepositories.Contains(delivery.PullRequestRepository, StringComparer.OrdinalIgnoreCase))
            return DeliveryOperationResult<PullRequestObservation>.Fail("github_mcp_repository_not_allowed", "Repository is not allowed for GitHub MCP integration.");
        try
        {
            var pull = Parse(await mcp.CallToolAsync("pull_request_read", new
            {
                method = "get",
                owner = parts[0],
                repo = parts[1],
                pullNumber = delivery.PullRequestNumber.Value
            }, ct));
            if (!string.Equals(pull.HeadSha, delivery.CommitSha, StringComparison.OrdinalIgnoreCase) || !string.Equals(pull.BaseBranch, delivery.PullRequestBaseBranch, StringComparison.Ordinal) || pull.Merged || !string.Equals(pull.State, "open", StringComparison.OrdinalIgnoreCase))
                return DeliveryOperationResult<PullRequestObservation>.Fail("delivery_pull_request_identity_changed", "Pull-request identity is no longer safe to merge.");
            if (pull.HasConflicts)
                return DeliveryOperationResult<PullRequestObservation>.Fail("delivery_pull_request_conflict", "Task pull request has merge conflicts.");
            if (pull.Draft)
                await mcp.CallToolAsync("update_pull_request", new
                {
                    owner = parts[0],
                    repo = parts[1],
                    pullNumber = delivery.PullRequestNumber.Value,
                    draft = false
                }, ct);
            var checks = await mcp.CallToolAsync("pull_request_read", new
            {
                method = "get_check_runs",
                owner = parts[0],
                repo = parts[1],
                pullNumber = delivery.PullRequestNumber.Value
            }, ct);
            var checksState = CheckRunsState(checks);
            if (checksState != "passed")
                return DeliveryOperationResult<PullRequestObservation>.Fail(checksState == "failed" ? "delivery_required_checks_failed" : "delivery_required_checks_pending", checksState == "failed" ? "Task pull-request checks failed." : "Task pull-request checks are still pending.");
            await mcp.CallToolAsync("merge_pull_request", new
            {
                owner = parts[0],
                repo = parts[1],
                pullNumber = delivery.PullRequestNumber.Value,
                merge_method = "squash",
                sha = delivery.CommitSha
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { }
        return await ReadAsync(delivery, ct);
    }

    public async Task<DeliveryOperationResult<FinalPullRequestReference>> OpenAsync(RunDelivery delivery, string title, string body, CancellationToken ct)
    {
        if (delivery.Status != RunDeliveryStatus.ReadyForFinalPullRequest || delivery.FinalReviewDecisionId is null || !string.Equals(delivery.FinalReviewedHeadSha, delivery.RunBranchHeadSha, StringComparison.OrdinalIgnoreCase))
            return DeliveryOperationResult<FinalPullRequestReference>.Fail("final_pull_request_state_invalid", "A current exact-head final review is required.");
        var project = await projects.GetAsync(delivery.ProjectId, ct);
        var repository = project is null ? null : RepositoryIdentity(project.RepositoryUrl);
        if (repository is null || !options.AllowedRepositories.Contains(repository, StringComparer.OrdinalIgnoreCase))
            return DeliveryOperationResult<FinalPullRequestReference>.Fail("github_mcp_repository_not_allowed", "Repository is not allowed for final delivery.");
        var parts = repository.Split('/');
        try
        {
            var pull = await FindAsync(parts[0], parts[1], delivery.SourceDefaultBranch, delivery.RunBranchName, ct);
            if (pull is null)
            {
                var created = await mcp.CallToolAsync("create_pull_request", new
                {
                    owner = parts[0],
                    repo = parts[1],
                    title,
                    head = delivery.RunBranchName,
                    @base = delivery.SourceDefaultBranch,
                    body,
                    draft = false,
                    maintainer_can_modify = true
                }, ct);
                var number = Long(created, "number") ?? Long(created, "pull_number") ?? throw new InvalidOperationException("github_mcp_malformed_response");
                pull = Parse(await mcp.CallToolAsync("pull_request_read", new
                {
                    method = "get",
                    owner = parts[0],
                    repo = parts[1],
                    pullNumber = number
                }, ct));
            }
            if (!string.Equals(pull.HeadSha, delivery.RunBranchHeadSha, StringComparison.OrdinalIgnoreCase) || !string.Equals(pull.BaseBranch, delivery.SourceDefaultBranch, StringComparison.Ordinal) || pull.Merged || !string.Equals(pull.State, "open", StringComparison.OrdinalIgnoreCase))
                return DeliveryOperationResult<FinalPullRequestReference>.Fail("final_pull_request_identity_conflict", "Final pull-request identity does not match the reviewed run head.");
            return DeliveryOperationResult<FinalPullRequestReference>.Ok(new($"GitHubMCP:{mcp.ServerIdentity}", repository, pull.Number, pull.Url, pull.HeadSha, pull.BaseBranch));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return DeliveryOperationResult<FinalPullRequestReference>.Fail("github_mcp_failed", "Final pull-request creation failed safely."); }
    }

    public async Task<DeliveryOperationResult<FinalPullRequestObservation>> ReadAsync(RunDelivery delivery, CancellationToken ct)
    {
        if (delivery.FinalPullRequestNumber is null || string.IsNullOrWhiteSpace(delivery.FinalPullRequestRepository))
            return DeliveryOperationResult<FinalPullRequestObservation>.Fail("final_pull_request_identity_missing", "Final pull-request identity is unavailable.");
        var parts = delivery.FinalPullRequestRepository.Split('/');
        try
        {
            var pull = Parse(await mcp.CallToolAsync("pull_request_read", new
            {
                method = "get",
                owner = parts[0],
                repo = parts[1],
                pullNumber = delivery.FinalPullRequestNumber.Value
            }, ct));
            if (!string.Equals(pull.HeadSha, delivery.RunBranchHeadSha, StringComparison.OrdinalIgnoreCase) || !string.Equals(pull.BaseBranch, delivery.SourceDefaultBranch, StringComparison.Ordinal))
                return DeliveryOperationResult<FinalPullRequestObservation>.Fail("final_pull_request_identity_conflict", "Final pull request no longer matches the reviewed run head.");
            var status = await mcp.CallToolAsync("pull_request_read", new
            {
                method = "get_status",
                owner = parts[0],
                repo = parts[1],
                pullNumber = delivery.FinalPullRequestNumber.Value
            }, ct);
            return DeliveryOperationResult<FinalPullRequestObservation>.Ok(new(pull.HeadSha, string.Equals(pull.State, "open", StringComparison.OrdinalIgnoreCase), pull.Merged, pull.MergeableState, ChecksState(status), pull.MergeCommitSha));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return DeliveryOperationResult<FinalPullRequestObservation>.Fail("github_mcp_failed", "Final pull-request readiness failed safely."); }
    }

    public async Task<DeliveryOperationResult<FinalRunMergeReference>> MergeAsync(RunDelivery delivery, CancellationToken ct)
    {
        if (delivery.Status != RunDeliveryStatus.MergeRequested || delivery.FinalPullRequestNumber is null || string.IsNullOrWhiteSpace(delivery.FinalPullRequestRepository) || string.IsNullOrWhiteSpace(delivery.FinalPullRequestHeadSha))
            return DeliveryOperationResult<FinalRunMergeReference>.Fail("final_merge_state_invalid", "Final merge requires explicit persisted approval.");
        var parts = delivery.FinalPullRequestRepository.Split('/');
        try
        {
            await mcp.CallToolAsync("merge_pull_request", new
            {
                owner = parts[0],
                repo = parts[1],
                pullNumber = delivery.FinalPullRequestNumber.Value,
                merge_method = "squash",
                sha = delivery.FinalPullRequestHeadSha
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { }
        var observed = await ReadAsync(delivery, ct);
        return observed.Succeeded && observed.Value!.Merged && !string.IsNullOrWhiteSpace(observed.Value.MergeCommitSha)
            ? DeliveryOperationResult<FinalRunMergeReference>.Ok(new(delivery.FinalPullRequestRepository, delivery.FinalPullRequestNumber.Value, delivery.FinalPullRequestHeadSha, observed.Value.MergeCommitSha))
            : DeliveryOperationResult<FinalRunMergeReference>.Fail("final_merge_not_confirmed", "Final pull-request merge was not confirmed.");
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
        var mergeable = value.TryGetProperty("mergeable", out var mergeableValue) ? mergeableValue : default;
        var mergeableState = Text(value, "mergeable_state") ?? Text(value, "mergeableState");
        var conflicts = mergeable.ValueKind == JsonValueKind.False || string.Equals(mergeableState, "dirty", StringComparison.OrdinalIgnoreCase);
        var readiness = conflicts ? "conflicting" : mergeable.ValueKind == JsonValueKind.True || string.Equals(mergeableState, "clean", StringComparison.OrdinalIgnoreCase) ? "mergeable" : "pending";
        return new(number, Text(value, "html_url") ?? Text(value, "htmlUrl") ?? Text(value, "url") ?? "", Text(value, "state") ?? "", value.TryGetProperty("merged", out var merged) && merged.ValueKind == JsonValueKind.True, value.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True, Text(head, "ref") ?? Text(value, "head_branch") ?? "", Text(@base, "ref") ?? Text(value, "base_branch") ?? "", Text(head, "sha") ?? Text(value, "head_sha") ?? "", Text(@base, "sha") ?? Text(value, "base_sha") ?? "", created, Text(value, "merge_commit_sha") ?? Text(value, "mergeCommitSha"), conflicts, readiness);
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
    private static string DiffText(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? "";
        foreach (var name in new[] { "diff", "content", "result" })
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String)
                return item.GetString() ?? "";
        throw new InvalidOperationException("github_mcp_malformed_response");
    }
    private static string ChecksState(JsonElement value)
    {
        var state = Text(value, "state") ?? Text(value, "status") ?? Text(value, "overall_state") ?? Text(value, "overallState");
        return state?.ToLowerInvariant() switch
        {
            "success" or "passed" => "passed",
            "failure" or "failed" or "error" => "failed",
            _ => "pending"
        };
    }
    private static string CheckRunsState(JsonElement value)
    {
        var runs = value.ValueKind == JsonValueKind.Array ? value : value.ValueKind == JsonValueKind.Object && value.TryGetProperty("check_runs", out var nested) ? nested : default;
        if (runs.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("github_mcp_malformed_response");
        var states = runs.EnumerateArray().Select(x => (Status: Text(x, "status"), Conclusion: Text(x, "conclusion"))).ToArray();
        if (states.Any(x => x.Conclusion is "failure" or "cancelled" or "timed_out" or "action_required" or "startup_failure"))
            return "failed";
        return states.Any(x => !string.Equals(x.Status, "completed", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(x.Conclusion)) ? "pending" : "passed";
    }
    private sealed record PullRequest(long Number, string Url, string State, bool Merged, bool Draft, string HeadBranch, string BaseBranch, string HeadSha, string BaseSha, DateTimeOffset CreatedAt, string? MergeCommitSha, bool HasConflicts, string MergeableState);
}
