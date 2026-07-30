using System.Diagnostics;
using System.Text;
using Impersonate.Application.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Execution;

internal sealed class SafeRepositoryTools(RepositoryWorkspaceService workspaces, IOptions<ExecutionOptions> options, SafeProcess processes) : IRepositoryTools
{
    private static readonly HashSet<string> Executables = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet",
        "node",
        "npm",
        "npx",
        "git"
    };
    private static readonly HashSet<string> GitCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "status",
        "diff",
        "apply",
        "rev-parse",
        "ls-files",
        "checkout",
        "clone"
    };
    private readonly int limit = options.Value.MaximumToolOutputCharacters;
    public Task<RepositoryToolResult> ListFilesAsync(WorkspaceReference workspace, string relativePath, CancellationToken ct)
    {
        var root = workspaces.FromReference(workspace);
        var path = Resolve(root, relativePath, true);
        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Where(x => !x.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}")).Take(1000).Select(x => Path.GetRelativePath(root, x).Replace('\\', '/'));
        return Task.FromResult(Ok(string.Join('\n', files)));
    }

    public async Task<RepositoryToolResult> ReadFileAsync(WorkspaceReference workspace, string relativePath, CancellationToken ct)
    {
        var root = workspaces.FromReference(workspace);
        if (IsSensitive(relativePath))
            return Fail("tool_rejected", "Credential and repository metadata files cannot be read.");
        var path = Resolve(root, relativePath, false);
        var info = new FileInfo(path);
        if (info.Length > 1_000_000)
            return Fail("tool_rejected", "File exceeds the read limit.");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        if (bytes.AsSpan().IndexOf((byte)0) >= 0)
            return Fail("tool_rejected", "Binary files are not supported.");
        return Ok(Bound(Encoding.UTF8.GetString(bytes), out var truncated), truncated);
    }

    public async Task<RepositoryToolResult> SearchTextAsync(WorkspaceReference workspace, string query, string relativePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Fail("tool_rejected", "Search query is required.");
        var root = workspaces.FromReference(workspace);
        var path = Resolve(root, relativePath, true);
        var results = new List<string>();
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Where(x => !x.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}")))
        {
            ct.ThrowIfCancellationRequested();
            if (new FileInfo(file).Length > 1_000_000)
                continue;
            string text;
            try
            {
                text = await File.ReadAllTextAsync(file, ct);
            }
            catch
            {
                continue;
            }

            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length && results.Count < 200; i++)
                if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                    results.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{i + 1}:{lines[i].TrimEnd()}");
            if (results.Count >= 200)
                break;
        }

        return Ok(Bound(string.Join('\n', results), out var truncated), truncated);
    }

    public async Task<RepositoryToolResult> ApplyPatchAsync(WorkspaceReference workspace, string patch, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(patch) || patch.IndexOf('\0') >= 0)
            return Fail("tool_rejected", "A UTF-8 text patch is required.");
        if (patch.Contains("GIT binary patch", StringComparison.Ordinal) || patch.Contains("Binary files ", StringComparison.Ordinal))
            return Fail("tool_rejected", "Binary patches are not supported.");
        if (patch.Contains("../", StringComparison.Ordinal) || patch.Contains("..\\", StringComparison.Ordinal))
            return Fail("tool_rejected", "Patch traversal is not permitted.");
        var result = await processes.RunAsync("git", Git(["apply", "--whitespace=nowarn", "-"]), workspaces.FromReference(workspace), 120, limit, patch, ct);
        return result.Succeeded ? Ok(result.Output) : Fail("tool_rejected", Bound(result.Output, out _));
    }

    public async Task<RepositoryToolResult> GetDiffAsync(WorkspaceReference workspace, CancellationToken ct)
    {
        var root = workspaces.FromReference(workspace);
        var intent = await processes.RunAsync("git", Git(["add", "--intent-to-add", "--", "."]), root, 120, limit, null, ct);
        if (!intent.Succeeded)
            return Fail("incremental_patch_generation_failed", "The current task file set could not be prepared for diffing.");
        var result = await processes.RunAsync("git", Git(["diff", "--no-ext-diff", "--find-renames", "--"]), root, 120, limit, null, ct);
        if (result.Output.Contains("GIT binary patch", StringComparison.Ordinal) || result.Output.Contains("Binary files ", StringComparison.Ordinal))
            return Fail("incremental_patch_generation_failed", "Binary patches are not supported.");
        return result.Succeeded ? Ok(Bound(result.Output, out var truncated), truncated) : Fail("incremental_patch_generation_failed", "The incremental task patch could not be generated.");
    }

    public async Task<RepositoryToolResult> RunCommandAsync(WorkspaceReference workspace, RepositoryCommand command, CancellationToken ct)
    {
        if (!Executables.Contains(command.Executable))
            return Fail("tool_rejected", "Executable is not allowed.");
        var isGit = command.Executable.Equals("git", StringComparison.OrdinalIgnoreCase);
        if (isGit && (command.Arguments.Count == 0 || !GitCommands.Contains(command.Arguments[0])))
            return Fail("tool_rejected", "Git subcommand is not allowed.");
        if (command.TimeoutSeconds is < 1 or > 600)
            return Fail("tool_rejected", "Command timeout is outside the allowed range.");
        var root = workspaces.FromReference(workspace);
        var cwd = Resolve(root, command.WorkingDirectory ?? ".", true);
        var result = await processes.RunAsync(command.Executable, isGit ? Git(command.Arguments) : command.Arguments, cwd, command.TimeoutSeconds, limit, null, ct);
        return result.TimedOut ? Fail("tool_timeout", "Command timed out.") : new(result.Succeeded, Bound(result.Output, out var truncated), result.Succeeded ? null : "command_failed", result.Succeeded ? null : "Command exited unsuccessfully.", truncated);
    }

    private string Resolve(string root, string relative, bool directory)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/', '\\').Contains(".."))
            throw new ArgumentException("Repository path is invalid.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (path != root && !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Repository path escapes the workspace.");
        for (var current = path; current.StartsWith(root, StringComparison.OrdinalIgnoreCase); current = Path.GetDirectoryName(current)!)
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                var attributes = File.GetAttributes(current);
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new ArgumentException("Symbolic-link paths are not permitted.");
            }

            if (current == root)
                break;
        }

        if (directory && !Directory.Exists(path))
            throw new DirectoryNotFoundException();
        if (!directory && !File.Exists(path))
            throw new FileNotFoundException();
        return path;
    }

    private static IReadOnlyList<string> Git(IReadOnlyList<string> arguments) => ["-c", "core.longpaths=true", .. arguments];
    private string Bound(string value, out bool truncated)
    {
        truncated = value.Length > limit;
        return truncated ? value[..limit] : value;
    }

    private static bool IsSensitive(string path) => path.Split('/', '\\').Any(x => x.Equals(".git", StringComparison.OrdinalIgnoreCase) || x.Equals(".env", StringComparison.OrdinalIgnoreCase) || x.Contains("credential", StringComparison.OrdinalIgnoreCase));
    private static RepositoryToolResult Ok(string output, bool truncated = false) => new(true, output, null, null, truncated);
    private static RepositoryToolResult Fail(string code, string message) => new(false, string.Empty, code, message);
}
