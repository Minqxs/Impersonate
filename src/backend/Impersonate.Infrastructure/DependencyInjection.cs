using Impersonate.Infrastructure.Persistence;
using Impersonate.Application.Projects;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Infrastructure.Agents.Planner;
using Impersonate.Application.Ai;
using Impersonate.Infrastructure.Ai;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Impersonate.Application.Execution;
using Impersonate.Infrastructure.Execution;
using Impersonate.Infrastructure.Agents.Execution;

namespace Impersonate.Infrastructure;

/// <summary>Registers infrastructure-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds configured infrastructure services without forcing database access at startup.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<ExecutionOptions>().BindConfiguration("Execution").Validate(x=>x.MaximumArtifactBytes is>=1024 and<=10_000_000&&x.MaximumToolOutputCharacters is>=1000 and<=1_000_000&&x.MaximumCoderSteps is>=1 and<=100&&x.CommandTimeoutSeconds is>=1 and<=600&&x.ClaimMinutes is>=1 and<=120,"Execution limits are invalid.").Validate(x=>environment.IsDevelopment()||environment.IsEnvironment("Testing")||(!string.IsNullOrWhiteSpace(x.WorkspaceRoot)&&!string.IsNullOrWhiteSpace(x.ArtifactRoot)),"Production requires explicit durable execution roots.").ValidateOnStart();
        services.AddSingleton<IExecutionArtifactStore,LocalExecutionArtifactStore>();services.AddSingleton<RepositoryWorkspaceService>();services.AddSingleton<IRepositoryWorkspaceService>(x=>x.GetRequiredService<RepositoryWorkspaceService>());services.AddSingleton<IRepositoryTools,SafeRepositoryTools>();services.AddScoped<ICoderAgent,CoderAgent>();services.AddScoped<IReviewerAgent,ReviewerAgent>();
        var connectionString = configuration.GetConnectionString("ImpersonateDatabase");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ImpersonateDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<IProjectRepository, EfProjectRepository>();
            services.AddScoped<IPipelineRunRepository, EfPipelineRunRepository>();
            services.AddScoped<IAiRoutingRepository, EfAiRoutingRepository>();
            services.AddScoped<IProviderCredentialStore, DataProtectionCredentialStore>();
            services.AddScoped<IModelUsageService, ModelUsageService>();
        }

        var allowDevelopmentDefault = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var keyPath = DataProtectionKeyPathResolver.Resolve(configuration["Ai:DataProtectionKeyPath"], allowDevelopmentDefault);
        services.AddSingleton(new DataProtectionKeyRingLocation(keyPath));
        services.AddDataProtection().SetApplicationName("Impersonate").PersistKeysToFileSystem(new DirectoryInfo(keyPath));

        services.AddHttpClient<AnthropicProviderAdapter>(x => { x.BaseAddress = new("https://api.anthropic.com/"); x.Timeout = TimeSpan.FromSeconds(120); });
        services.AddHttpClient<OpenAiProviderAdapter>(x => { x.BaseAddress = new("https://api.openai.com/"); x.Timeout = TimeSpan.FromSeconds(120); });
        services.AddHttpClient<GeminiProviderAdapter>(x => { x.BaseAddress = new("https://generativelanguage.googleapis.com/"); x.Timeout = TimeSpan.FromSeconds(120); });
        services.AddHttpClient<OpenRouterProviderAdapter>(x => { x.BaseAddress = new("https://openrouter.ai/api/"); x.Timeout = TimeSpan.FromSeconds(120); });
        services.AddScoped<IAiProviderAdapter>(x => x.GetRequiredService<AnthropicProviderAdapter>());
        services.AddScoped<IAiProviderAdapter>(x => x.GetRequiredService<OpenAiProviderAdapter>());
        services.AddScoped<IAiProviderAdapter>(x => x.GetRequiredService<GeminiProviderAdapter>());
        services.AddScoped<IAiProviderAdapter>(x => x.GetRequiredService<OpenRouterProviderAdapter>());

        services.AddHttpClient<ILanguageModelClient, AnthropicLanguageModelClient>((provider,client)=>{var options=provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlannerOptions>>().Value;client.BaseAddress=new Uri("https://api.anthropic.com/");client.Timeout=TimeSpan.FromSeconds(options.TimeoutSeconds);var key=configuration["ANTHROPIC_API_KEY"]??configuration["Anthropic:ApiKey"];if(!string.IsNullOrWhiteSpace(key))client.DefaultRequestHeaders.Add("x-api-key",key);});
        services.AddScoped<IPlannerAgent, PlannerAgent>();
        services.AddScoped<IPlanningRepositoryContextService,PlanningRepositoryContextService>();
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
