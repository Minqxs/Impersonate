using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Impersonate.Application.Delivery;
using Impersonate.Application.Execution;
using Impersonate.Application.Projects;
using Impersonate.Domain.Delivery;
using Impersonate.Infrastructure.Execution;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery;

internal sealed partial class LocalTargetRepositoryDeliveryService(IProjectRepository projects, ITaskDeliveryRepository deliveries, IExecutionArtifactStore artifacts, IDeliveryValidationService validation, DeliveryWorkspaceRegistry workspaces, SafeProcess process, IOptions<ExecutionOptions> options) : ITargetRepositoryDeliveryService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    public async Task<DeliveryOperationResult<TargetRepositoryDeliveryResult>> DeliverApprovedPatchAsync(TaskDelivery delivery, ApprovedTaskHandoff handoff, CancellationToken ct)
    {
        try
        {
            if (delivery.Status != TaskDeliveryStatus.Pending) return DeliveryOperationResult<TargetRepositoryDeliveryResult>.Fail("delivery_recovery_required", "Delivery must be recovered before retrying local preparation.");
            if (delivery.ProjectId != handoff.ProjectId || delivery.PipelineRunId != handoff.PipelineRunId || delivery.PlannedTaskId != handoff.PlannedTaskId || !string.Equals(delivery.ApprovedPatchSha256, handoff.ApprovedPatchSha256, StringComparison.OrdinalIgnoreCase)) return DeliveryOperationResult<TargetRepositoryDeliveryResult>.Fail("delivery_handoff_identity_mismatch", "Delivery and approved handoff identities do not match.");
            var gate = Gates.GetOrAdd(delivery.ProjectId, _ => new(1, 1));
            await gate.WaitAsync(ct);
            try { return DeliveryOperationResult<TargetRepositoryDeliveryResult>.Ok(await DeliverLockedAsync(delivery, handoff, ct)); }
            finally { gate.Release(); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { var code = ex.Message.Split(':', 2)[0]; return DeliveryOperationResult<TargetRepositoryDeliveryResult>.Fail(code.StartsWith("delivery_", StringComparison.Ordinal) ? code : "delivery_failed", "Local delivery preparation failed safely."); }
    }

    private async Task<TargetRepositoryDeliveryResult> DeliverLockedAsync(TaskDelivery delivery, ApprovedTaskHandoff handoff, CancellationToken ct)
    {
        var project = await projects.GetAsync(delivery.ProjectId, ct) ?? throw new InvalidOperationException("delivery_project_not_found");
        var root = Path.GetFullPath(options.Value.DeliveryRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Impersonate", "delivery"));
        var cache = Path.Combine(root, "repositories", delivery.ProjectId.ToString("N"), "repository.git");
        var workspace = Path.Combine(root, "worktrees", delivery.ProjectId.ToString("N"), delivery.Id.ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        Directory.CreateDirectory(Path.GetDirectoryName(workspace)!);
        if (!Directory.Exists(cache)) await GitAsync(root, ["clone", "--bare", "--no-tags", "--", project.RepositoryUrl, cache], null, ct);
        await GitAsync(cache, ["fetch", "--no-tags", "origin", $"+refs/heads/{project.DefaultBranch}:refs/remotes/origin/{project.DefaultBranch}"], null, ct);
        var deliveryBase = (await GitAsync(cache, ["rev-parse", $"refs/remotes/origin/{project.DefaultBranch}^{{commit}}"], null, ct)).Trim();
        var branch = TaskBranchNameGenerator.Create(delivery.PipelineRunId, delivery.TaskSequence, handoff.Title, delivery.ApprovedPatchSha256);
        await GitAsync(cache, ["check-ref-format", "--branch", branch], null, ct);

        // Explicit recovery may leave only delivery-owned local effects. Remove those exact
        // deterministic effects before rebuilding; never reset or clean an arbitrary checkout.
        if (Directory.Exists(workspace)) await GitAsync(cache, ["worktree", "remove", "--force", workspace], null, ct);
        var branchExists = await process.RunAsync("git", ["show-ref", "--verify", "--quiet", $"refs/heads/{branch}"], cache, options.Value.CommandTimeoutSeconds, 1000, null, ct);
        if (branchExists.Succeeded) await GitAsync(cache, ["branch", "-D", branch], null, ct);

        delivery.StartPreparing(); delivery.RecordDeliveryBase(deliveryBase); await deliveries.SaveChangesAsync(ct);
        await GitAsync(cache, ["worktree", "add", "-b", branch, workspace, deliveryBase], null, ct);
        delivery.RecordBranchPrepared(branch); await deliveries.SaveChangesAsync(ct);

        var patch = await artifacts.ReadTextAsync(handoff.ApprovedPatchArtifactReference, options.Value.MaximumArtifactBytes, ct);
        VerifyPatch(patch, handoff);
        await GitAsync(workspace, ["apply", "--check", "--whitespace=error", "-"], patch, ct);
        await GitAsync(workspace, ["apply", "--index", "--whitespace=error", "-"], patch, ct);
        var staged = Lines(await GitAsync(workspace, ["diff", "--cached", "--name-only", "--diff-filter=ACDMRTUXB"], null, ct));
        if (staged.Count == 0 || !staged.ToHashSet(StringComparer.Ordinal).SetEquals(handoff.ChangedFiles)) throw new InvalidOperationException("delivery_changed_files_mismatch");
        delivery.RecordPatchApplied(); await deliveries.SaveChangesAsync(ct);

        var reference = workspaces.Register(workspace);
        IReadOnlyList<DeliveryValidationStep> checks;
        try
        {
            var result = await validation.ValidateAsync(reference, ct);
            if (!result.Succeeded) throw new InvalidOperationException(result.Code ?? "delivery_validation_failed");
            checks = result.Value!;
        }
        finally { workspaces.Remove(reference); }
        var validationJson = JsonSerializer.Serialize(checks);
        delivery.RecordValidated(validationJson); await deliveries.SaveChangesAsync(ct);

        var subject = $"task({delivery.TaskSequence}): {handoff.Title}";
        await GitAsync(workspace, ["-c", $"user.name={options.Value.DeliveryCommitName}", "-c", $"user.email={options.Value.DeliveryCommitEmail}", "commit", "-m", subject], null, ct);
        var commit = (await GitAsync(workspace, ["rev-parse", "HEAD^{commit}"], null, ct)).Trim();
        var parents = (await GitAsync(workspace, ["show", "-s", "--format=%P", commit], null, ct)).Split([' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parents.Length != 1 || !string.Equals(parents[0], deliveryBase, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("delivery_commit_parent_mismatch");
        delivery.RecordCommitted(commit); await deliveries.SaveChangesAsync(ct);
        return new(branch, deliveryBase, commit, checks);
    }

    private async Task<string> GitAsync(string cwd, IReadOnlyList<string> args, string? input, CancellationToken ct)
    {
        var result = await process.RunAsync("git", args, cwd, options.Value.CommandTimeoutSeconds, options.Value.MaximumToolOutputCharacters, input, ct);
        if (!result.Succeeded) throw new InvalidOperationException(result.TimedOut ? "delivery_git_timeout" : "delivery_git_failed");
        return result.Output;
    }

    private static void VerifyPatch(string patch, ApprovedTaskHandoff handoff)
    {
        if (string.IsNullOrWhiteSpace(patch) || patch.IndexOf('\0') >= 0 || patch.Contains("GIT binary patch", StringComparison.Ordinal) || patch.Contains("Subproject commit ", StringComparison.Ordinal)) throw new InvalidOperationException("delivery_patch_unsafe");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(patch))).ToLowerInvariant();
        if (!string.Equals(hash, handoff.ApprovedPatchSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("delivery_patch_hash_mismatch");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in DiffHeader().Matches(patch))
        {
            var left = match.Groups[1].Value; var right = match.Groups[2].Value;
            if (!SafePath(left) || !SafePath(right)) throw new InvalidOperationException("delivery_patch_path_unsafe");
            names.Add(right);
        }
        if (names.Count == 0 || !names.SetEquals(handoff.ChangedFiles)) throw new InvalidOperationException("delivery_changed_files_mismatch");
    }
    private static bool SafePath(string path) => path.Length > 0 && !Path.IsPathRooted(path) && !path.Split('/', '\\').Contains("..") && !path.Contains('\\');
    private static IReadOnlyList<string> Lines(string value) => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    [GeneratedRegex(@"^diff --git a/([^\r\n]+) b/([^\r\n]+)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)] private static partial Regex DiffHeader();
}
