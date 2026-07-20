using Impersonate.Infrastructure.Persistence;
using Impersonate.Application.Projects;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Infrastructure.Agents.Planner;
using Impersonate.Application.AiModels;
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
            services.AddScoped<IAiModelConfigurationRepository, EfAiModelConfigurationRepository>();
        }

        services.AddHttpClient<ILanguageModelClient, AnthropicLanguageModelClient>((provider,client)=>{var options=provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlannerOptions>>().Value;client.BaseAddress=new Uri("https://api.anthropic.com/");client.Timeout=TimeSpan.FromSeconds(options.TimeoutSeconds);var key=configuration["ANTHROPIC_API_KEY"]??configuration["Anthropic:ApiKey"];if(!string.IsNullOrWhiteSpace(key))client.DefaultRequestHeaders.Add("x-api-key",key);});
        services.AddScoped<IPlannerAgent, PlannerAgent>();
        services.AddSingleton<IProviderRuntimeStatus>(new ProviderRuntimeStatus(configuration));

        return services;
    }
}

internal sealed class ProviderRuntimeStatus(IConfiguration configuration):IProviderRuntimeStatus
{
 public bool IsSupported(string provider)=>string.Equals(provider,"Anthropic",StringComparison.OrdinalIgnoreCase);
 public bool CredentialsConfigured(string provider)=>IsSupported(provider)&&!string.IsNullOrWhiteSpace(configuration["ANTHROPIC_API_KEY"]??configuration["Anthropic:ApiKey"]);
}
