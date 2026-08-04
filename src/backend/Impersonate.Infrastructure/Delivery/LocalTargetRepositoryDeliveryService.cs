using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Impersonate.Application.Delivery;
using Impersonate.Application.Execution;
using Impersonate.Application.Projects;
using Impersonate.Domain.Delivery;
using Impersonate.Infrastructure.Execution;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery;

internal sealed class LocalTargetRepositoryDeliveryService(IProjectRepository projects, ITaskDeliveryRepository deliveries, IRunDeliveryRepository runDeliveries, IExecutionArtifactStore artifacts, IDeliveryValidationService validation, DeliveryWorkspaceRegistry workspaces, SafeProcess process, IOptions<ExecutionOptions> options) : ITargetRepositoryDeliveryService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    public async Task<DeliveryOperationResult<TargetRepositoryDeliveryResult>> DeliverApprovedPatchAsync(TaskDelivery delivery, ApprovedTaskHandoff handoff, CancellationToken ct)
    {
        try
        {
            if (delivery.Status is < TaskDeliveryStatus.Pending or > TaskDeliveryStatus.Committed)
                return Fail("delivery_state_invalid", "Delivery is not locally recoverable in its current state.");
            if (delivery.ProjectId != handoff.ProjectId || delivery.PipelineRunId != handoff.PipelineRunId || delivery.PlannedTaskId != handoff.PlannedTaskId || !string.Equals(delivery.ApprovedPatchSha256, handoff.ApprovedPatchSha256, StringComparison.OrdinalIgnoreCase))
                return Fail("delivery_handoff_identity_mismatch", "Delivery and approved handoff identities do not match.");
            var gate = Gates.GetOrAdd(delivery.ProjectId, _ => new(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                return DeliveryOperationResult<TargetRepositoryDeliveryResult>.Ok(await DeliverLockedAsync(delivery, handoff, ct));
            }
            finally { gate.Release(); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var parts = ex.Message.Split(':', 2);
            var code = parts[0];
            return Fail(code.StartsWith("delivery_", StringComparison.Ordinal) ? code : "delivery_failed", parts.Length == 2 ? parts[1] : "Local delivery preparation failed safely.");
        }
    }

    private async Task<TargetRepositoryDeliveryResult> DeliverLockedAsync(TaskDelivery delivery, ApprovedTaskHandoff handoff, CancellationToken ct)
    {
        var project = await projects.GetAsync(delivery.ProjectId, ct) ?? throw new InvalidOperationException("delivery_project_not_found");
        var runDelivery = await runDeliveries.GetByRunAsync(delivery.ProjectId, delivery.PipelineRunId, ct) ?? throw new InvalidOperationException("run_delivery_not_found");
        if (runDelivery.Status != RunDeliveryStatus.IntegratingTasks || string.IsNullOrWhiteSpace(runDelivery.RunBranchHeadSha))
            throw new InvalidOperationException("run_delivery_branch_not_ready");
        var root = Path.GetFullPath(options.Value.DeliveryRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Impersonate", "delivery"));
        var cache = Path.Combine(root, "repositories", delivery.ProjectId.ToString("N"), "repository.git");
        var workspace = Path.Combine(root, "worktrees", delivery.ProjectId.ToString("N"), delivery.Id.ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        Directory.CreateDirectory(Path.GetDirectoryName(workspace)!);
        if (!Directory.Exists(cache))
            await GitAsync(root, ["clone", "--bare", "--no-tags", "--", project.RepositoryUrl, cache], null, ct);
        await GitAsync(cache, ["fetch", "--no-tags", "origin", $"+refs/heads/{runDelivery.RunBranchName}:refs/remotes/origin/{runDelivery.RunBranchName}"], null, ct);
        var remoteBase = (await GitAsync(cache, ["rev-parse", $"refs/remotes/origin/{runDelivery.RunBranchName}^{{commit}}"], null, ct)).Trim();
        if (!string.Equals(remoteBase, runDelivery.RunBranchHeadSha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("run_delivery_branch_head_changed");
        var branch = delivery.BranchName ?? TaskBranchNameGenerator.Create(delivery.PipelineRunId, delivery.TaskSequence, handoff.Title, delivery.ApprovedPatchSha256);
        await GitAsync(cache, ["check-ref-format", "--branch", branch], null, ct);

        if (delivery.Status == TaskDeliveryStatus.Pending)
        {
            delivery.StartPreparing();
            delivery.RecordDeliveryBase(remoteBase);
            delivery.RecordBranchIntent(branch);
            await deliveries.SaveChangesAsync(ct);
        }
        var deliveryBase = delivery.DeliveryBaseCommitSha ?? throw new InvalidOperationException("delivery_base_missing");
        if (delivery.Status == TaskDeliveryStatus.Preparing && !string.Equals(deliveryBase, remoteBase, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("delivery_base_changed");
        await EnsureWorkspaceAsync(cache, workspace, branch, deliveryBase, delivery.Status, ct);
        if (delivery.Status == TaskDeliveryStatus.Preparing)
        {
            delivery.RecordBranchPrepared(branch);
            await deliveries.SaveChangesAsync(ct);
        }

        var patch = await artifacts.ReadTextAsync(handoff.ApprovedPatchArtifactReference, options.Value.MaximumArtifactBytes, ct);
        VerifyPatch(patch, handoff);
        var gitPatch = patch.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (delivery.Status == TaskDeliveryStatus.BranchPrepared)
        {
            var staged = await StagedFilesAsync(workspace, ct);
            if (staged.Count == 0)
            {
                await GitAsync(workspace, ["apply", "--check", "--whitespace=error", "-"], gitPatch, ct);
                await GitAsync(workspace, ["apply", "--index", "--whitespace=error", "-"], gitPatch, ct);
                staged = await StagedFilesAsync(workspace, ct);
            }
            RepositoryFileSetVerifier.Verify("staged", staged, handoff.ChangedFiles);
            delivery.RecordPatchApplied();
            await deliveries.SaveChangesAsync(ct);
        }
        IReadOnlyList<DeliveryValidationStep> checks = DeserializeValidation(delivery.ValidationSummaryJson);
        if (delivery.Status == TaskDeliveryStatus.PatchApplied)
        {
            await VerifyApprovedIndexAsync(workspace, handoff.ChangedFiles, ct);
            var reference = workspaces.Register(workspace);
            try
            {
                var result = await validation.ValidateAsync(reference, ct);
                if (!result.Succeeded)
                    throw new InvalidOperationException(result.Code ?? "delivery_validation_failed");
                checks = result.Value!;
            }
            finally { workspaces.Remove(reference); }
            await VerifyApprovedIndexAsync(workspace, handoff.ChangedFiles, ct);
            delivery.RecordValidated(JsonSerializer.Serialize(checks));
            await deliveries.SaveChangesAsync(ct);
        }

        if (delivery.Status == TaskDeliveryStatus.Validated)
        {
            var head = (await GitAsync(workspace, ["rev-parse", "HEAD^{commit}"], null, ct)).Trim();
            string commit;
            if (string.Equals(head, deliveryBase, StringComparison.OrdinalIgnoreCase))
            {
                await VerifyApprovedIndexAsync(workspace, handoff.ChangedFiles, ct);
                await GitAsync(workspace, ["-c", $"user.name={options.Value.DeliveryCommitName}", "-c", $"user.email={options.Value.DeliveryCommitEmail}", "commit", "-m", $"task({delivery.TaskSequence}): {handoff.Title}"], null, ct);
                commit = (await GitAsync(workspace, ["rev-parse", "HEAD^{commit}"], null, ct)).Trim();
            }
            else
                commit = head;
            await VerifyCommitAsync(workspace, commit, deliveryBase, handoff.ChangedFiles, ct);
            delivery.RecordCommitted(commit);
            await deliveries.SaveChangesAsync(ct);
        }
        var recordedCommit = delivery.CommitSha ?? throw new InvalidOperationException("delivery_commit_missing");
        await VerifyCommitAsync(workspace, recordedCommit, deliveryBase, handoff.ChangedFiles, ct);
        await GitAsync(cache, ["worktree", "remove", "--force", workspace], null, ct);
        return new(branch, deliveryBase, recordedCommit, checks);
    }

    private async Task EnsureWorkspaceAsync(string cache, string workspace, string branch, string deliveryBase, TaskDeliveryStatus status, CancellationToken ct)
    {
        var branchSha = await TryGitAsync(cache, ["rev-parse", $"refs/heads/{branch}^{{commit}}"], ct);
        if (!Directory.Exists(workspace))
        {
            if (branchSha is null)
                await GitAsync(cache, ["worktree", "add", "-b", branch, workspace, deliveryBase], null, ct);
            else
                await GitAsync(cache, ["worktree", "add", workspace, branch], null, ct);
        }
        var actualBranch = (await GitAsync(workspace, ["branch", "--show-current"], null, ct)).Trim();
        if (!string.Equals(actualBranch, branch, StringComparison.Ordinal))
            throw new InvalidOperationException("delivery_branch_conflict");
        var head = (await GitAsync(workspace, ["rev-parse", "HEAD^{commit}"], null, ct)).Trim();
        if (status is TaskDeliveryStatus.Preparing or TaskDeliveryStatus.BranchPrepared or TaskDeliveryStatus.PatchApplied or TaskDeliveryStatus.Validated && !string.Equals(head, deliveryBase, StringComparison.OrdinalIgnoreCase))
        {
            if (status != TaskDeliveryStatus.Validated)
                throw new InvalidOperationException("delivery_branch_conflict");
        }
    }

    private async Task VerifyApprovedIndexAsync(string workspace, IReadOnlyList<string> approved, CancellationToken ct)
    {
        RepositoryFileSetVerifier.Verify("staged", await StagedFilesAsync(workspace, ct), approved);
        if (Lines(await GitAsync(workspace, ["diff", "--name-only"], null, ct)).Count != 0)
            throw new InvalidOperationException("delivery_unapproved_worktree_change");
        if (Lines(await GitAsync(workspace, ["ls-files", "--others", "--exclude-standard"], null, ct)).Count != 0)
            throw new InvalidOperationException("delivery_validation_generated_files");
    }
    private async Task VerifyCommitAsync(string workspace, string commit, string deliveryBase, IReadOnlyList<string> approved, CancellationToken ct)
    {
        var parents = (await GitAsync(workspace, ["show", "-s", "--format=%P", commit], null, ct)).Split([' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parents.Length != 1 || !string.Equals(parents[0], deliveryBase, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("delivery_commit_parent_mismatch");
        RepositoryFileSetVerifier.Verify("commit", Lines(await GitAsync(workspace, ["-c", "core.quotePath=false", "diff-tree", "--no-commit-id", "--name-only", "-r", "-z", commit], null, ct), true), approved);
        var head = (await GitAsync(workspace, ["rev-parse", "HEAD^{commit}"], null, ct)).Trim();
        if (!string.Equals(head, commit, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("delivery_commit_identity_conflict");
    }
    private async Task<IReadOnlyList<string>> StagedFilesAsync(string workspace, CancellationToken ct) => Lines(await GitAsync(workspace, ["-c", "core.quotePath=false", "diff", "--cached", "--name-only", "-z", "--diff-filter=ACDMRTUXB", "--"], null, ct), true);
    private async Task<string?> TryGitAsync(string cwd, IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await process.RunAsync("git", args, cwd, options.Value.CommandTimeoutSeconds, 1000, null, ct);
        return result.Succeeded ? result.Output.Trim() : null;
    }
    private async Task<string> GitAsync(string cwd, IReadOnlyList<string> args, string? input, CancellationToken ct)
    {
        var result = await process.RunAsync("git", args, cwd, options.Value.CommandTimeoutSeconds, options.Value.MaximumToolOutputCharacters, input, ct);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.TimedOut ? "delivery_git_timeout" : "delivery_git_failed");
        return result.Output;
    }
    private static void VerifyPatch(string patch, ApprovedTaskHandoff handoff)
    {
        if (string.IsNullOrWhiteSpace(patch) || patch.IndexOf('\0') >= 0 || patch.Contains("GIT binary patch", StringComparison.Ordinal) || patch.Contains("Subproject commit ", StringComparison.Ordinal))
            throw new InvalidOperationException("delivery_patch_unsafe");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(patch))).ToLowerInvariant();
        if (!string.Equals(hash, handoff.ApprovedPatchSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("delivery_patch_hash_mismatch");
        var names = GitPatchPathParser.Parse(patch);
        var approved = RepositoryPathCanonicalizer.Set(handoff.ChangedFiles);
        RepositoryFileSetVerifier.Verify("patch", names, approved);
    }
    private static IReadOnlyList<DeliveryValidationStep> DeserializeValidation(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<DeliveryValidationStep>>(json) ?? [];
        }
        catch { return []; }
    }
    private static IReadOnlyList<string> Lines(string value, bool nul = false) => value.Split(nul ? ['\0'] : ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static DeliveryOperationResult<TargetRepositoryDeliveryResult> Fail(string code, string error) => DeliveryOperationResult<TargetRepositoryDeliveryResult>.Fail(code, error);
}
