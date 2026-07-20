using Microsoft.Extensions.DependencyInjection;
using Impersonate.Application.Projects;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;

namespace Impersonate.Application;

/// <summary>Registers application-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds the application layer to a service collection.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services.AddScoped<IProjectService, ProjectService>().AddScoped<IPipelineRunService, PipelineRunService>().AddSingleton<ILoopDefinitionRegistry, FeatureDeliveryLoopRegistry>().AddOptions<PipelineOptions>().BindConfiguration("Pipeline:FeatureDelivery").Validate(x => x.MaximumRevisionAttempts is >= 0 and <= 20, "MaximumRevisionAttempts must be between 0 and 20.").ValidateOnStart().Services.AddOptions<PlannerOptions>().BindConfiguration("Agents:Planner").Validate(x=>x.MaximumTasks is>=1 and<=50&&x.MaximumPlanningAttempts is>=1 and<=5&&x.TimeoutSeconds is>=10 and<=600,"Planner limits are invalid.").Services;
}
