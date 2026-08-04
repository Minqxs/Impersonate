using Impersonate.Application.Ai;
using Impersonate.Application.Delivery;
using Impersonate.Application.Execution;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Application.Projects;
using Impersonate.Application.Quality;
using Impersonate.Infrastructure.Agents.Execution;
using Impersonate.Infrastructure.Agents.Planner;
using Impersonate.Infrastructure.Ai;
using Impersonate.Infrastructure.Delivery;
using Impersonate.Infrastructure.Delivery.Mcp;
using Impersonate.Infrastructure.Execution;
using Impersonate.Infrastructure.Persistence;
using Impersonate.Infrastructure.Quality;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Impersonate.Infrastructure;
/// <summary>Registers infrastructure-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds configured infrastructure services without forcing database access at startup.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<ExecutionOptions>().BindConfiguration("Execution").Validate(x => x.MaximumArtifactBytes is >= 1024 and <= 10_000_000 && x.MaximumToolOutputCharacters is >= 1000 and <= 1_000_000 && x.MaximumCoderProviderRounds is >= 1 and <= 100 && x.MaximumCoderToolExecutions is >= 1 and <= 500 && x.DefaultCoderMaximumOutputTokens is >= 1000 and <= 100_000 && x.DefaultReviewerMaximumOutputTokens is >= 1000 and <= 100_000 && x.DefaultModelContextWindowTokens is >= 8000 and <= 2_000_000 && x.CommandTimeoutSeconds is >= 1 and <= 600 && x.ClaimMinutes is >= 1 and <= 120 && x.MaximumWorkspacePreparationAttempts is >= 1 and <= 5 && x.MaximumSameModelRateLimitRetries is >= 0 and <= 10 && x.MaximumAutomaticRateLimitWaitSeconds is >= 0 and <= 300 && x.MaximumTotalRateLimitWaitSecondsPerOperation is >= 0 and <= 900 && x.InitialRateLimitBackoffMilliseconds is >= 1 and <= 60_000 && x.MaximumRateLimitBackoffSeconds is >= 1 and <= 300 && x.RateLimitJitterMaximumMilliseconds is >= 0 and <= 10_000, "Execution limits are invalid.").Validate(x => environment.IsDevelopment() || environment.IsEnvironment("Testing") || (!string.IsNullOrWhiteSpace(x.WorkspaceRoot) && !string.IsNullOrWhiteSpace(x.ArtifactRoot)), "Production requires explicit durable execution roots.").ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<IProjectQualityCache, MemoryProjectQualityCache>();
        services.AddSingleton<ProviderCapacityCoordinator>();
        services.AddSingleton<IChildProcessEnvironmentBuilder, AllowlistedChildProcessEnvironmentBuilder>();
        services.AddSingleton<SafeProcess>();
        services.AddSingleton<IExecutionEnvironmentReadinessService, ExecutionEnvironmentReadinessService>();
        services.AddSingleton<IExecutionArtifactStore, LocalExecutionArtifactStore>();
        services.AddSingleton<DeliveryWorkspaceRegistry>();
        services.AddSingleton<IDeliveryValidationService, ConservativeDeliveryValidationService>();
        services.AddScoped<ITargetRepositoryDeliveryService, LocalTargetRepositoryDeliveryService>();
        services.AddScoped<ITaskDeliveryPushService, TaskDeliveryPushService>();
        services.AddScoped<ITaskDeliveryRepairer, LocalTaskDeliveryRepairer>();
        services.AddScoped<IRunIntegrationService, LocalRunIntegrationService>();
        services.AddOptions<GitHubMcpOptions>().Configure(x => x.Tools = []).BindConfiguration("Delivery:GitHubMcp").Validate(x => !x.Enabled || (x.TimeoutSeconds is >= 1 and <= 300 && (x.Transport == "Remote" || x.Transport == "Local") && x.Tools.Length == 3 && x.Tools.Contains("list_pull_requests") && x.Tools.Contains("pull_request_read") && x.Tools.Contains("create_pull_request")), "GitHub MCP delivery configuration is invalid.").ValidateOnStart();
        services.AddHttpClient<RemoteOfficialGitHubMcpClient>((provider, client) => client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GitHubMcpOptions>>().Value.TimeoutSeconds)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddTransient<LocalOfficialGitHubMcpClient>();
        services.AddScoped<IGitHubMcpClient>(provider => string.Equals(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GitHubMcpOptions>>().Value.Transport, "Local", StringComparison.OrdinalIgnoreCase) ? provider.GetRequiredService<LocalOfficialGitHubMcpClient>() : provider.GetRequiredService<RemoteOfficialGitHubMcpClient>());
        services.AddScoped<IPullRequestGateway, GitHubMcpPullRequestGateway>();
        services.AddSingleton<RepositoryWorkspaceService>();
        services.AddSingleton<IRepositoryWorkspaceService>(x => x.GetRequiredService<RepositoryWorkspaceService>());
        services.AddSingleton<IRepositoryTools, SafeRepositoryTools>();
        services.AddScoped<ICoderAgent, CoderAgent>();
        services.AddScoped<IReviewerAgent, ReviewerAgent>();
        var connectionString = configuration.GetConnectionString("ImpersonateDatabase");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ImpersonateDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<IProjectRepository, EfProjectRepository>();
            services.AddScoped<IPipelineRunRepository, EfPipelineRunRepository>();
            services.AddScoped<ITaskDeliveryRepository, EfTaskDeliveryRepository>();
            services.AddScoped<IRunDeliveryRepository, EfRunDeliveryRepository>();
            services.AddScoped<ITaskDeliveryReviewRepository, EfTaskDeliveryReviewRepository>();
            services.AddScoped<IExecutionInvocationStore, EfExecutionInvocationStore>();
            services.AddScoped<IAiRoutingRepository, EfAiRoutingRepository>();
            services.AddScoped<IProviderCredentialStore, DataProtectionCredentialStore>();
            services.AddScoped<IProjectQualityRepository, EfProjectQualityRepository>();
            services.AddScoped<ICodeQualityCredentialStore, DataProtectionCodeQualityCredentialStore>();
            services.AddScoped<IModelUsageService, ModelUsageService>();
        }

        var allowDevelopmentDefault = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var keyPath = DataProtectionKeyPathResolver.Resolve(configuration["Ai:DataProtectionKeyPath"], allowDevelopmentDefault);
        services.AddSingleton(new DataProtectionKeyRingLocation(keyPath));
        services.AddDataProtection().SetApplicationName("Impersonate").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        services.AddOptions<SonarQubeOptions>().BindConfiguration("CodeQuality:SonarQube").Validate(x => x.TimeoutSeconds is >= 1 and <= 60, "SonarQube timeout is invalid.").ValidateOnStart();
        services.AddSingleton<ISonarQubeEndpointPolicy, SonarQubeEndpointPolicy>();
        services.AddHttpClient<ICodeQualityProvider, SonarQubeProvider>((provider, client) => client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SonarQubeOptions>>().Value.TimeoutSeconds)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<AnthropicProviderAdapter>(x =>
        {
            x.BaseAddress = new("https://api.anthropic.com/");
            x.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddHttpClient<OpenAiProviderAdapter>(x =>
        {
            x.BaseAddress = new("https://api.openai.com/");
            x.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddHttpClient<GeminiProviderAdapter>(x =>
        {
            x.BaseAddress = new("https://generativelanguage.googleapis.com/");
            x.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddHttpClient<OpenRouterProviderAdapter>(x =>
        {
            x.BaseAddress = new("https://openrouter.ai/api/");
            x.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddScoped<IAiProviderAdapter>(x => x.GetRequiredService<AnthropicProviderAdapter>());
        services.AddScoped<IAiProviderAdapter>(x => x.GetRequiredService<OpenAiProviderAdapter>());
        services.AddScoped<IAiProviderAdapter>(x => x.GetRequiredService<GeminiProviderAdapter>());
        services.AddScoped<IAiProviderAdapter>(x => x.GetRequiredService<OpenRouterProviderAdapter>());
        services.AddHttpClient<ILanguageModelClient, AnthropicLanguageModelClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlannerOptions>>().Value;
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            var key = configuration["ANTHROPIC_API_KEY"] ?? configuration["Anthropic:ApiKey"];
            if (!string.IsNullOrWhiteSpace(key))
                client.DefaultRequestHeaders.Add("x-api-key", key);
        });
        services.AddScoped<IPlannerAgent, PlannerAgent>();
        services.AddScoped<IPlanningRepositoryContextService, PlanningRepositoryContextService>();
        services.AddSingleton<IPlannerReadiness>(new PlannerReadinessService(configuration));
        return services;
    }
}
