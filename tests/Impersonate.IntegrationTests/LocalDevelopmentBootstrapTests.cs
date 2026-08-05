using System.Text.Json;
using Impersonate.Infrastructure.Delivery.Mcp;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class LocalDevelopmentBootstrapTests
{
    [Theory]
    [InlineData("https://github.com/Minqxs/TaskIt", "Minqxs/TaskIt")]
    [InlineData("https://github.com/Minqxs/TaskIt.git", "Minqxs/TaskIt")]
    [InlineData("https://github.com/minqxs/taskit/", "minqxs/taskit")]
    [InlineData("  Minqxs/TaskIt  ", "Minqxs/TaskIt")]
    public void Repository_identity_is_normalized(string value, string expected) => Assert.Equal(expected, GitHubRepositoryIdentity.Normalize(value));

    [Theory]
    [InlineData("")]
    [InlineData("owner")]
    [InlineData("owner/repo/extra")]
    [InlineData("https://example.com/owner/repo")]
    [InlineData("owner/re po")]
    public void Malformed_repository_identity_is_rejected(string value) => Assert.Null(GitHubRepositoryIdentity.Normalize(value));

    [Fact]
    public void Development_configuration_allows_TaskIt_without_a_secret()
    {
        using var api = JsonDocument.Parse(File.ReadAllText(Repo("src", "backend", "Impersonate.Api", "appsettings.Development.json")));
        using var worker = JsonDocument.Parse(File.ReadAllText(Repo("src", "backend", "Impersonate.Worker", "appsettings.Development.json")));
        foreach (var document in new[] { api, worker })
        {
            var mcp = document.RootElement.GetProperty("Delivery").GetProperty("GitHubMcp");
            Assert.True(mcp.GetProperty("Enabled").GetBoolean());
            Assert.Contains("Minqxs/TaskIt", mcp.GetProperty("AllowedRepositories").EnumerateArray().Select(x => x.GetString()));
            Assert.False(mcp.TryGetProperty("Token", out _));
        }
    }

    [Fact]
    public void Production_defaults_disable_delivery_and_do_not_allow_repositories()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Repo("src", "backend", "Impersonate.Worker", "appsettings.json")));
        var mcp = document.RootElement.GetProperty("Delivery").GetProperty("GitHubMcp");
        Assert.False(mcp.GetProperty("Enabled").GetBoolean());
        Assert.Empty(mcp.GetProperty("AllowedRepositories").EnumerateArray());
    }

    [Fact]
    public void Bootstrap_accepts_no_token_parameter_and_scopes_process_shutdown()
    {
        var start = File.ReadAllText(Repo("scripts", "local", "start-impersonate.ps1"));
        var stop = File.ReadAllText(Repo("scripts", "local", "stop-impersonate.ps1"));
        var lifecycle = File.ReadAllText(Repo("scripts", "local", "process-lifecycle.ps1"));
        Assert.DoesNotContain("[string]$GitHub", start, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$env:GITHUB_MCP_TOKEN", start);
        Assert.DoesNotContain("Invoke-Expression", start, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stop-AllImpersonateLocalProcesses", stop);
        Assert.Contains("StartTimeUtc", lifecycle);
        Assert.Contains("ExecutablePath", lifecycle);
        Assert.Contains("RepositoryRoot", lifecycle);
        Assert.Contains("Launcher", lifecycle);
        Assert.Contains("Test-ImpersonateMetadata", lifecycle);
        Assert.Contains("CloseMainWindow", lifecycle);
        Assert.Contains("Stop-Process -Id $process.Id -Force", lifecycle);
        Assert.DoesNotContain("Stop-Process -Name dotnet", lifecycle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GITHUB_MCP_TOKEN", lifecycle);
    }

    [Fact]
    public void Rider_compound_owns_both_dotnet_projects_without_detached_bootstrap()
    {
        var api = File.ReadAllText(Repo(".run", "Impersonate API.run.xml"));
        var worker = File.ReadAllText(Repo(".run", "Impersonate Worker.run.xml"));
        var compound = File.ReadAllText(Repo(".run", "Impersonate Local.run.xml"));
        Assert.Contains("type=\"DotNetProject\"", api);
        Assert.Contains("Impersonate.Api.csproj", api);
        Assert.Contains("type=\"DotNetProject\"", worker);
        Assert.Contains("Impersonate.Worker.csproj", worker);
        Assert.Contains("Impersonate API", compound);
        Assert.Contains("Impersonate Worker", compound);
        Assert.DoesNotContain("start-impersonate.ps1", api + worker + compound, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Start-Process", api + worker + compound, StringComparison.OrdinalIgnoreCase);
    }

    private static string Repo(params string[] parts)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine([root, .. parts]);
    }
}
