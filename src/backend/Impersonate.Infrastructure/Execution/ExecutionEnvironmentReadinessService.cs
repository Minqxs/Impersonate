using System.Diagnostics;
using System.Text;
using Impersonate.Application.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Execution;

internal sealed class ExecutionEnvironmentReadinessService(IChildProcessEnvironmentBuilder environments, SafeProcess processes, IOptions<ExecutionOptions> options) : IExecutionEnvironmentReadinessService
{
    public async Task<ExecutionEnvironmentReadiness> CheckAsync(CancellationToken ct)
    {
        var supplied = environments.Build();
        var blockers = new List<string>();
        var core = !OperatingSystem.IsWindows() || supplied.ContainsKey("SystemRoot");
        if (!core)
            blockers.Add("SystemRoot is unavailable in the Windows child-process environment.");
        var root = Path.GetFullPath(options.Value.WorkspaceRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Impersonate", "workspaces"));
        var writable = false;
        try
        {
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, $".readiness-{Guid.NewGuid():N}");
            await File.WriteAllTextAsync(probe, "ready", ct);
            File.Delete(probe);
            writable = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            blockers.Add("The configured workspace root is not writable.");
        }

        var git = await processes.RunAsync("git", ["--version"], root, 15, 1000, null, ct);
        if (git.StartFailure)
            blockers.Add("Git is not available.");
        else if (!git.Succeeded)
            blockers.Add("Git version validation failed.");
        return new(blockers.Count == 0, System.Runtime.InteropServices.RuntimeInformation.OSDescription, !git.StartFailure, git.Succeeded, writable, core, git.Succeeded, supplied.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), blockers);
    }
}
