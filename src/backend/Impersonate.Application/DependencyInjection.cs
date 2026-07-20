using Impersonate.Application.AiModels;using Impersonate.Application.Pipelines;using Impersonate.Application.Planning;using Impersonate.Application.Projects;using Microsoft.Extensions.DependencyInjection;
namespace Impersonate.Application;
public static class DependencyInjection
{
 public static IServiceCollection AddApplication(this IServiceCollection services)=>services.AddScoped<IProjectService,ProjectService>().AddScoped<IPipelineRunService,PipelineRunService>().AddScoped<IAiModelConfigurationService,AiModelConfigurationService>().AddScoped<IAgentModelResolver,AgentModelResolver>().AddSingleton<ILoopDefinitionRegistry,FeatureDeliveryLoopRegistry>().AddOptions<PipelineOptions>().BindConfiguration("Pipeline:FeatureDelivery").Validate(x=>x.MaximumRevisionAttempts is>=0 and<=20,"MaximumRevisionAttempts must be between 0 and 20.").ValidateOnStart().Services.AddOptions<PlannerOptions>().BindConfiguration("Agents:Planner").Validate(x=>x.MaximumTasks is>=1 and<=50&&x.MaximumPlanningAttempts is>=1 and<=5&&x.TimeoutSeconds is>=10 and<=600,"Planner limits are invalid.").Services;
}
