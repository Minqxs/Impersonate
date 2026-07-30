using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Application.Projects;
using Impersonate.Infrastructure.Agents.Execution;
using Impersonate.Infrastructure.Agents.Planner;
using Impersonate.Infrastructure.Ai;
using Impersonate.Infrastructure.Execution;
using Impersonate.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Impersonate.Infrastructure;

internal sealed class PlannerReadinessService(IConfiguration configuration) : IPlannerReadiness
{
    public PlannerReadiness Get()
    {
        var provider = configuration["Agents:Planner:Provider"];
        var model = configuration["Agents:Planner:Model"];
        var credentials = configuration["ANTHROPIC_API_KEY"] ?? configuration["Anthropic:ApiKey"];
        var providerConfigured = string.Equals(provider, "Anthropic", StringComparison.OrdinalIgnoreCase);
        var modelConfigured = !string.IsNullOrWhiteSpace(model);
        var credentialsConfigured = !string.IsNullOrWhiteSpace(credentials);
        var message = !providerConfigured ? "Planner provider must be configured as Anthropic." : !modelConfigured ? "Planner model is not configured." : !credentialsConfigured ? "Anthropic credentials are not configured." : "Planner configuration is ready. The Worker must use the same configuration.";
        return new(providerConfigured && modelConfigured && credentialsConfigured ? "Ready" : "Incomplete", providerConfigured, modelConfigured, credentialsConfigured, message);
    }
}
