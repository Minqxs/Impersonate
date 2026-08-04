using Impersonate.Application.Delivery;
using Impersonate.Application.Execution;
using Impersonate.Application.Projects;
using Impersonate.Domain.Delivery;
using Impersonate.Infrastructure.Execution;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery;

internal sealed class LocalRunIntegrationService(IProjectRepository projects, IRunDeliveryRepository deliveries, SafeProcess process, IOptions<ExecutionOptions> configured) : IRunIntegrationService
{
    public async Task<DeliveryOperationResult<RunIntegrationReference>> PrepareAsync(RunDelivery delivery, CancellationToken ct)
    {
        try
        {
            var project = await projects.GetAsync(delivery.ProjectId, ct) ?? throw new InvalidOperationException("run_delivery_project_not_found");
            var options = configured.Value;
            var root = Path.GetFullPath(options.DeliveryRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Impersonate", "delivery"));
            var cache = Path.Combine(root, "repositories", delivery.ProjectId.ToString("N"), "repository.git");
            Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
            if (!Directory.Exists(cache))
                await GitAsync(root, ["clone", "--bare", "--no-tags", "--", project.RepositoryUrl, cache], options, ct);
            await GitAsync(cache, ["fetch", "--no-tags", "origin", $"+refs/heads/{delivery.SourceDefaultBranch}:refs/remotes/origin/{delivery.SourceDefaultBranch}"], options, ct);
            var defaultHead = (await GitAsync(cache, ["rev-parse", $"refs/remotes/origin/{delivery.SourceDefaultBranch}^{{commit}}"], options, ct)).Trim();
            if (!string.Equals(defaultHead, delivery.SourceBaseCommitSha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("run_delivery_default_branch_changed");
            await GitAsync(cache, ["check-ref-format", "--branch", delivery.RunBranchName], options, ct);
            if (delivery.Status == RunDeliveryStatus.Pending)
            {
                delivery.StartPreparing();
                await deliveries.SaveChangesAsync(ct);
            }
            var remote = await process.RunAsync("git", ["ls-remote", "--heads", "origin", $"refs/heads/{delivery.RunBranchName}"], cache, options.CommandTimeoutSeconds, 1000, null, ct);
            if (!remote.Succeeded)
                throw new InvalidOperationException("run_delivery_remote_read_failed");
            var remoteHead = remote.Output.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(remoteHead))
            {
                await GitAsync(cache, ["push", "origin", $"{defaultHead}:refs/heads/{delivery.RunBranchName}"], options, ct);
                remoteHead = defaultHead;
            }
            if (!string.Equals(remoteHead, defaultHead, StringComparison.OrdinalIgnoreCase) && delivery.RunBranchHeadSha is null)
                throw new InvalidOperationException("run_delivery_branch_conflict");
            if (delivery.RunBranchHeadSha is not null && !string.Equals(remoteHead, delivery.RunBranchHeadSha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("run_delivery_branch_head_changed");
            if (delivery.Status == RunDeliveryStatus.Preparing)
            {
                delivery.RecordRunBranch(remoteHead);
                await deliveries.SaveChangesAsync(ct);
            }
            if (delivery.Status == RunDeliveryStatus.RunBranchCreated)
            {
                delivery.StartTaskIntegration();
                await deliveries.SaveChangesAsync(ct);
            }
            return DeliveryOperationResult<RunIntegrationReference>.Ok(new(project.RepositoryUrl, delivery.SourceDefaultBranch, defaultHead, delivery.RunBranchName, remoteHead));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var code = ex.Message.StartsWith("run_delivery_", StringComparison.Ordinal) ? ex.Message : "run_delivery_prepare_failed";
            return DeliveryOperationResult<RunIntegrationReference>.Fail(code, "Run integration branch could not be prepared safely.");
        }
    }

    private async Task<string> GitAsync(string workingDirectory, IReadOnlyList<string> arguments, ExecutionOptions options, CancellationToken ct)
    {
        var result = await process.RunAsync("git", arguments, workingDirectory, options.CommandTimeoutSeconds, options.MaximumToolOutputCharacters, null, ct);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.TimedOut ? "run_delivery_git_timeout" : "run_delivery_git_failed");
        return result.Output;
    }
}
