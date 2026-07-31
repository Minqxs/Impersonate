using Impersonate.Application.Ai;
using Impersonate.Application.Delivery;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Application.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace Impersonate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddScoped<IProjectService, ProjectService>()
        .AddScoped<IPipelineRunService, PipelineRunService>()
        .AddScoped<ITaskDeliveryCoordinator, TaskDeliveryCoordinator>()
        .AddScoped<ITaskDeliveryOrchestrator, TaskDeliveryOrchestrator>()
        .AddScoped<ITaskDeliveryReconciler, TaskDeliveryReconciler>()
        .AddScoped<Execution.ITaskExecutionOrchestrator, Execution.TaskExecutionOrchestrator>()
        .AddSingleton<ILoopDefinitionRegistry, FeatureDeliveryLoopRegistry>()
        .AddSingleton<IExecutionOrderService, ExecutionOrderService>()
        .AddSingleton<ITaskProfiler, DeterministicTaskProfiler>()
        .AddSingleton<IModelIdentityClassifier, ProviderAwareModelIdentityClassifier>()
        .AddSingleton<IModelCapabilityCatalog, VersionedModelCapabilityCatalog>()
        .AddScoped<IModelRouter, DeterministicModelRouter>()
        .AddScoped<IProjectAiService, ProjectAiService>()
        .AddScoped<IAiProviderConnectionService, AiProviderConnectionService>()
        .AddOptions<PipelineOptions>().BindConfiguration("Pipeline:FeatureDelivery").Validate(x => x.MaximumRevisionAttempts is >= 0 and <= 20, "MaximumRevisionAttempts must be between 0 and 20.").ValidateOnStart().Services
        .AddOptions<PlannerOptions>().BindConfiguration("Agents:Planner").Validate(x => x.MaximumTasks is >= 1 and <= 50 && x.MaximumPlanningAttempts is >= 1 and <= 5 && x.MaximumOutputTokens is >= 500 and <= 8000 && x.TimeoutSeconds is >= 10 and <= 600, "Planner limits are invalid.").Services;
}
