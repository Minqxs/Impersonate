using System.Diagnostics;
using System.Text;
using Impersonate.Application.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Execution;

internal sealed class RepositoryWorkspaceService : IRepositoryWorkspaceService
{
    private const string Prefix = "workspace:";
    private readonly string root;
    private readonly IExecutionArtifactStore artifacts;
    private readonly SafeProcess processes;
    private readonly int outputLimit, maximumAttempts;
    public RepositoryWorkspaceService(IOptions<ExecutionOptions> options, IExecutionArtifactStore artifacts, SafeProcess processes)
    {
        root = Path.GetFullPath(options.Value.WorkspaceRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Impersonate", "workspaces"));
        this.artifacts = artifacts;
        this.processes = processes;
        outputLimit = options.Value.MaximumToolOutputCharacters;
        maximumAttempts = options.Value.MaximumWorkspacePreparationAttempts;
        Directory.CreateDirectory(root);
    }

    public async Task<WorkspacePreparationResult> PrepareAsync(WorkspaceRequest request, CancellationToken ct)
    {
        var relative = Path.Combine(request.ProjectId.ToString("N"), request.PipelineRunId.ToString("N"), request.PlannedTaskId.ToString("N"), request.AttemptNumber.ToString());
        var path = Resolve(relative);
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            ProcessResult clone = default!;
            WorkspaceFailure failure = default!;
            for (var attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                clone = await processes.RunAsync("git", ["-c", "core.longpaths=true", "clone", "--no-tags", "--single-branch", "--branch", request.DefaultBranch, "--", request.RepositoryUrl, path], root, 300, outputLimit, null, ct);
                if (clone.Succeeded)
                    break;
                failure = Classify(clone);
                if (!failure.Transient || attempt == maximumAttempts)
                    return new(false, null, failure.Code, failure.Message);
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), ct);
            }

            var source = await processes.RunAsync("git", ["-c", "core.longpaths=true", "rev-parse", "HEAD"], path, 30, outputLimit, null, ct);
            if (!source.Succeeded)
                return new(false, null, "composed_baseline_creation_failed", "The source baseline could not be identified.");
            foreach (var dependency in request.ApprovedDependencyPatches.OrderBy(x => x.Sequence).ThenBy(x => x.TaskId))
            {
                string patch;
                try
                {
                    patch = await artifacts.ReadTextAsync(dependency.ArtifactReference, 2_000_000, ct);
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
                {
                    return new(false, null, "approved_dependency_patch_missing", $"Approved dependency task {dependency.Sequence} patch is unavailable.", source.Output.Trim(), null, request.ApprovedDependencyPatches.Select(x => x.TaskId).ToList(), false, dependency.Sequence);
                }

                var applied = await processes.RunAsync("git", ["-c", "core.longpaths=true", "apply", "--whitespace=nowarn", "-"], path, 120, outputLimit, patch, ct);
                if (!applied.Succeeded)
                    return new(false, null, "approved_dependency_patch_composition_failed", $"Approved dependency task {dependency.Sequence} patch could not be composed.", source.Output.Trim(), null, request.ApprovedDependencyPatches.Select(x => x.TaskId).ToList(), false, dependency.Sequence);
            }

            var staged = await processes.RunAsync("git", ["-c", "core.longpaths=true", "add", "-A", "--", "."], path, 120, outputLimit, null, ct);
            if (!staged.Succeeded)
                return new(false, null, "composed_baseline_creation_failed", "The composed dependency baseline could not be staged.", source.Output.Trim());
            var tree = await processes.RunAsync("git", ["-c", "core.longpaths=true", "write-tree"], path, 120, outputLimit, null, ct);
            if (!tree.Succeeded)
                return new(false, null, "composed_baseline_creation_failed", "The composed dependency tree fingerprint could not be created.", source.Output.Trim());
            var revisionApplied = false;
            if (request.CurrentPatchReference is not null)
            {
                string patch;
                try
                {
                    patch = await artifacts.ReadTextAsync(request.CurrentPatchReference, 2_000_000, ct);
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
                {
                    return new(false, null, "current_task_revision_patch_missing", "The current task revision patch is unavailable.", source.Output.Trim(), tree.Output.Trim(), request.ApprovedDependencyPatches.Select(x => x.TaskId).ToList());
                }

                var applied = await processes.RunAsync("git", ["-c", "core.longpaths=true", "apply", "--whitespace=nowarn", "-"], path, 120, outputLimit, patch, ct);
                if (!applied.Succeeded)
                    return new(false, null, "current_task_revision_patch_apply_failed", "The current task revision patch could not be applied.", source.Output.Trim(), tree.Output.Trim(), request.ApprovedDependencyPatches.Select(x => x.TaskId).ToList());
                revisionApplied = true;
            }

            return new(true, new(Prefix + relative.Replace('\\', '/')), null, null, source.Output.Trim(), tree.Output.Trim(), request.ApprovedDependencyPatches.Select(x => x.TaskId).ToList(), revisionApplied);
        }
        catch (UnauthorizedAccessException)
        {
            return new(false, null, "workspace_access_denied", "The configured workspace root is not accessible.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return new(false, null, "execution_environment_invalid", "The sanitized execution environment could not prepare the workspace root.");
        }
    }

    public Task CleanupAsync(WorkspaceReference workspace, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = FromReference(workspace);
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        return Task.CompletedTask;
    }

    internal string FromReference(WorkspaceReference reference)
    {
        if (!reference.Value.StartsWith(Prefix, StringComparison.Ordinal))
            throw new ArgumentException("Workspace reference is invalid.");
        return Resolve(reference.Value[Prefix.Length..].Replace('/', Path.DirectorySeparatorChar));
    }

    private string Resolve(string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
            throw new ArgumentException("Workspace path is invalid.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Workspace path escapes the configured root.");
        return path;
    }

    private static string Safe(string value) => value.Length <= 1000 ? value : value[..1000];
    private static WorkspaceFailure Classify(ProcessResult result)
    {
        if (result.StartFailure)
            return new("git_not_available", "Git could not be started in the sanitized execution environment.", false);
        if (result.TimedOut)
            return new("workspace_clone_timeout", "The isolated repository clone timed out.", true);
        var text = result.Output.ToLowerInvariant();
        if (text.Contains("getaddrinfo") || text.Contains("could not resolve host") || text.Contains("name or service not known"))
            return new("repository_dns_failed", "Repository DNS resolution failed while preparing the isolated workspace.", true);
        if (text.Contains("authentication failed") || text.Contains("could not read username") || text.Contains("terminal prompts disabled") || text.Contains("permission denied (publickey)"))
            return new("repository_authentication_required", "Repository authentication is required.", false);
        if (text.Contains("remote branch") && text.Contains("not found") || text.Contains("couldn't find remote ref"))
            return new("repository_branch_not_found", "The configured repository branch was not found.", false);
        if (text.Contains("access is denied") || text.Contains("permission denied"))
            return new("workspace_access_denied", "The configured workspace root is not accessible.", false);
        if (text.Contains("repository not found"))
            return new("repository_unavailable", "The configured repository is unavailable.", false);
        if (text.Contains("unable to access") || text.Contains("failed to connect") || text.Contains("connection"))
            return new("repository_unavailable", "The configured repository is temporarily unavailable.", true);
        return new("workspace_preparation_failed", "The isolated repository workspace could not be prepared.", false);
    }

    private sealed record WorkspaceFailure(string Code, string Message, bool Transient);
}
