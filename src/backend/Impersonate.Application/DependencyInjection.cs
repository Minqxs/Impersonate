using Microsoft.Extensions.DependencyInjection;
using Impersonate.Application.Projects;
using Impersonate.Application.Pipelines;

namespace Impersonate.Application;

/// <summary>Registers application-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds the application layer to a service collection.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services.AddScoped<IProjectService, ProjectService>().AddScoped<IPipelineRunService, PipelineRunService>().AddSingleton<ILoopDefinitionRegistry, FeatureDeliveryLoopRegistry>().AddOptions<PipelineOptions>().BindConfiguration("Pipeline:FeatureDelivery").Validate(x => x.MaximumRevisionAttempts is >= 0 and <= 20, "MaximumRevisionAttempts must be between 0 and 20.").ValidateOnStart().Services;
}
