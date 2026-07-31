using System.Collections.Concurrent;
using Impersonate.Application.Delivery;
using Impersonate.Application.Execution;
using Impersonate.Application.Projects;
using Impersonate.Domain.Delivery;
using Impersonate.Infrastructure.Execution;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery;

internal sealed class TaskDeliveryPushService(IProjectRepository projects, ITaskDeliveryRepository deliveries, SafeProcess process, IOptions<ExecutionOptions> options) : ITaskDeliveryPushService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    public async Task<DeliveryOperationResult<TaskDeliveryPushResult>> PushAsync(TaskDelivery delivery, CancellationToken ct)
    {
        if (delivery.Status == TaskDeliveryStatus.Pushed)
            return Existing(delivery, true);
        if (delivery.Status != TaskDeliveryStatus.Committed || string.IsNullOrWhiteSpace(delivery.BranchName) || string.IsNullOrWhiteSpace(delivery.CommitSha))
            return Fail("delivery_push_state_invalid", "Only a committed delivery with branch and commit identity can be pushed.");
        var project = await projects.GetAsync(delivery.ProjectId, ct);
        if (project is null) return Fail("delivery_project_not_found", "Delivery project was not found.");
        var repository = RepositoryIdentity(project.RepositoryUrl);
        if (repository is null) return Fail("delivery_repository_invalid", "Project repository identity is invalid.");
        var root = Path.GetFullPath(options.Value.DeliveryRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Impersonate", "delivery"));
        var cache = Path.Combine(root, "repositories", delivery.ProjectId.ToString("N"), "repository.git");
        if (!Directory.Exists(cache)) return Fail("delivery_cache_missing", "Local delivery repository cache is unavailable.");
        var gate = Gates.GetOrAdd(delivery.ProjectId, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var local = await RunAsync(cache, ["rev-parse", $"refs/heads/{delivery.BranchName}^{{commit}}"], ct);
            if (!local.Succeeded || !string.Equals(local.Output.Trim(), delivery.CommitSha, StringComparison.OrdinalIgnoreCase)) return Fail("delivery_local_branch_conflict", "Local task branch no longer points to the approved commit.");
            var fetch = await RunAsync(cache, ["fetch", "--prune", "--no-tags", "origin", "+refs/heads/*:refs/remotes/origin/*"], ct);
            if (!fetch.Succeeded) return Fail(Classify(fetch), "Remote refs could not be refreshed safely.");
            var remote = await RemoteShaAsync(cache, delivery.BranchName, ct);
            if (remote is not null && !string.Equals(remote, delivery.CommitSha, StringComparison.OrdinalIgnoreCase)) return Fail("delivery_remote_branch_conflict", "Remote task branch points to a different commit.");
            var recovered = remote is not null;
            if (!recovered)
            {
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    var push = await RunAsync(cache, ["push", "--set-upstream", "origin", $"{delivery.BranchName}:refs/heads/{delivery.BranchName}"], ct);
                    remote = await RemoteShaAsync(cache, delivery.BranchName, ct);
                    if (string.Equals(remote, delivery.CommitSha, StringComparison.OrdinalIgnoreCase)) { recovered = !push.Succeeded; break; }
                    if (remote is not null) return Fail("delivery_remote_branch_conflict", "Remote task branch points to a different commit.");
                    if (attempt == 3) return Fail(Classify(push), "Task branch could not be pushed safely.");
                }
            }
            delivery.RecordPushed("origin", repository, delivery.BranchName, delivery.CommitSha);
            await deliveries.SaveChangesAsync(ct);
            return DeliveryOperationResult<TaskDeliveryPushResult>.Ok(new("origin", repository, delivery.BranchName, delivery.CommitSha, recovered));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return Fail("delivery_push_failed", "Task branch could not be pushed safely."); }
        finally { gate.Release(); }
    }

    private async Task<string?> RemoteShaAsync(string cache, string branch, CancellationToken ct)
    {
        var result = await RunAsync(cache, ["ls-remote", "--heads", "origin", $"refs/heads/{branch}"], ct);
        if (!result.Succeeded) return null;
        var line = result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).SingleOrDefault();
        return line?.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }
    private Task<ProcessResult> RunAsync(string cwd, IReadOnlyList<string> arguments, CancellationToken ct) => process.RunAsync("git", arguments, cwd, options.Value.CommandTimeoutSeconds, 2000, null, ct);
    private static string Classify(ProcessResult result)
    {
        if (result.StartFailure) return "delivery_git_unavailable";
        if (result.TimedOut) return "delivery_push_timeout";
        return result.Output.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase) || result.Output.Contains("could not read Username", StringComparison.OrdinalIgnoreCase)
            ? "delivery_push_authentication_unavailable" : "delivery_push_failed";
    }
    private static string? RepositoryIdentity(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return null;
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2) return null;
        var repository = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        return $"{parts[0]}/{repository}";
    }
    private static DeliveryOperationResult<TaskDeliveryPushResult> Existing(TaskDelivery d, bool recovered) =>
        string.IsNullOrWhiteSpace(d.RemoteName) || string.IsNullOrWhiteSpace(d.RemoteRepository) || string.IsNullOrWhiteSpace(d.RemoteBranchName) || string.IsNullOrWhiteSpace(d.PushedCommitSha)
            ? Fail("delivery_push_identity_missing", "Pushed delivery identity is incomplete.")
            : DeliveryOperationResult<TaskDeliveryPushResult>.Ok(new(d.RemoteName, d.RemoteRepository, d.RemoteBranchName, d.PushedCommitSha, recovered));
    private static DeliveryOperationResult<TaskDeliveryPushResult> Fail(string code, string error) => DeliveryOperationResult<TaskDeliveryPushResult>.Fail(code, error);
}
