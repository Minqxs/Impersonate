using Impersonate.Infrastructure.Persistence;
using Impersonate.Application.Projects;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Infrastructure.Agents.Planner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Impersonate.Infrastructure;

/// <summary>Registers infrastructure-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds configured infrastructure services without forcing database access at startup.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ImpersonateDatabase");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ImpersonateDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<IProjectRepository, EfProjectRepository>();
            services.AddScoped<IPipelineRunRepository, EfPipelineRunRepository>();
        }

        services.AddHttpClient<ILanguageModelClient, AnthropicLanguageModelClient>((provider,client)=>{var options=provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlannerOptions>>().Value;client.BaseAddress=new Uri("https://api.anthropic.com/");client.Timeout=TimeSpan.FromSeconds(options.TimeoutSeconds);var key=configuration["ANTHROPIC_API_KEY"]??configuration["Anthropic:ApiKey"];if(!string.IsNullOrWhiteSpace(key))client.DefaultRequestHeaders.Add("x-api-key",key);});
        services.AddScoped<IPlannerAgent, PlannerAgent>();
        services.AddSingleton<IPlannerReadiness>(new PlannerReadinessService(configuration));

        return services;
    }
}

internal sealed class PlannerReadinessService(IConfiguration configuration):IPlannerReadiness
{
    public PlannerReadiness Get()
    {
        var provider=configuration["Agents:Planner:Provider"];
        var model=configuration["Agents:Planner:Model"];
        var credentials=configuration["ANTHROPIC_API_KEY"]??configuration["Anthropic:ApiKey"];
        var providerConfigured=string.Equals(provider,"Anthropic",StringComparison.OrdinalIgnoreCase);
        var modelConfigured=!string.IsNullOrWhiteSpace(model);
        var credentialsConfigured=!string.IsNullOrWhiteSpace(credentials);
        var message=!providerConfigured?"Planner provider must be configured as Anthropic.":!modelConfigured?"Planner model is not configured.":!credentialsConfigured?"Anthropic credentials are not configured.":"Planner configuration is ready. The Worker must use the same configuration.";
        return new(providerConfigured&&modelConfigured&&credentialsConfigured?"Ready":"Incomplete",providerConfigured,modelConfigured,credentialsConfigured,message);
    }
}
