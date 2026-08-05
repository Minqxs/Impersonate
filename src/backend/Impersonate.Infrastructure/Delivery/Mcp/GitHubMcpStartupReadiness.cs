using Impersonate.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery.Mcp;

public sealed class GitHubMcpStartupReadiness(IOptions<GitHubMcpOptions> configured, IHostEnvironment environment, IConfiguration configuration, DataProtectionKeyRingLocation keys, ILogger<GitHubMcpStartupReadiness> logger) : IHostedService
{
    public GitHubMcpReadiness Get()
    {
        var options = configured.Value;
        return new(options.Enabled, options.Transport, options.ServerId, options.AllowedRepositories, options.Tools, options.TokenEnvironmentVariable,
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(options.TokenEnvironmentVariable)), environment.EnvironmentName, keys.Path,
            !string.IsNullOrWhiteSpace(configuration.GetConnectionString("ImpersonateDatabase")));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var value = Get();
        logger.LogInformation("Local readiness: environment={Environment}; GitHub MCP enabled={Enabled}; transport={Transport}; server={ServerId}; repositories={Repositories}; tools={Tools}; token variable={TokenVariable}; token available={TokenAvailable}; Data Protection keys={KeyLocation}; database configured={DatabaseConfigured}", value.Environment, value.Enabled, value.Transport, value.ServerId, string.Join(",", value.AllowedRepositories), string.Join(",", value.Tools), value.TokenEnvironmentVariable, value.TokenAvailable, value.DataProtectionKeyLocation, value.DatabaseConfigured);
        if (value.Enabled && !value.TokenAvailable)
            throw new InvalidOperationException("github_mcp_token_missing");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
