using Impersonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Impersonate.Infrastructure.Delivery.Mcp;

public sealed class DevelopmentPreflightService(IServiceProvider services, GitHubMcpStartupReadiness readiness)
{
    private static readonly string[] RequiredTools = ["list_pull_requests", "pull_request_read", "create_pull_request", "update_pull_request", "merge_pull_request"];
    public async Task<DevelopmentPreflight> CheckAsync(string targetRepository, CancellationToken ct)
    {
        var safe = readiness.Get();
        var normalized = GitHubRepositoryIdentity.Normalize(targetRepository);
        var connected = false;
        var current = false;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<ImpersonateDbContext>();
        if (db is not null)
        {
            connected = await db.Database.CanConnectAsync(ct);
            current = connected && !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
        }
        return new(connected, current, TryWrite(safe.DataProtectionKeyLocation), CommandOnPath("git.exe") || CommandOnPath("git"), safe.Enabled, normalized is not null, normalized is not null && safe.AllowedRepositories.Contains(normalized, StringComparer.OrdinalIgnoreCase), safe.TokenAvailable, safe.ServerId == "github-official" && safe.Transport.Equals("Remote", StringComparison.OrdinalIgnoreCase), RequiredTools.All(x => safe.Tools.Contains(x, StringComparer.Ordinal)));
    }
    private static bool CommandOnPath(string name) => (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Any(x => File.Exists(Path.Combine(x, name)));
    private static bool TryWrite(string path)
    {
        try
        {
            var file = Path.Combine(path, $".preflight-{Guid.NewGuid():N}");
            File.WriteAllText(file, "");
            File.Delete(file);
            return true;
        }
        catch { return false; }
    }
}
