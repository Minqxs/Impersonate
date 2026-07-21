using Impersonate.Application;
using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Application.Projects;
using Impersonate.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Impersonate.Application.Tests.Ai;

public sealed class ModelRouterTests
{
    [Fact] public async Task Selects_only_connected_available_capable_models_deterministically()
    {
        var project=Guid.NewGuid();var connected=AiProviderConnection.Create(ProviderType.Anthropic,"Anthropic");connected.Connected();
        var model=DiscoveredModel.Create(connected.Id,ProviderType.Anthropic,"claude-test","Claude",null,ModelLifecycleStatus.Stable,CapabilityMetadataSource.VersionedProviderMapping,"7",200000,8192);
        var services=new ServiceCollection().AddApplication().AddSingleton<IAiRoutingRepository>(new FakeRepository([connected],[model],ProjectAiRoutingPolicy.Create(project))).BuildServiceProvider();
        var router=services.GetRequiredService<IModelRouter>();var first=await router.SelectAsync(new(project,null,AgentRole.Planner,"Create a small settings page."),default);var second=await router.SelectAsync(new(project,null,AgentRole.Planner,"Create a small settings page."),default);
        Assert.True(first.Succeeded);Assert.Equal(model.Id,first.Selection!.DiscoveredModelId);Assert.Equal(first.Selection,second.Selection);
    }
    [Fact] public async Task Rejects_invalid_manual_override()
    {
        var project=Guid.NewGuid();var services=new ServiceCollection().AddApplication().AddSingleton<IAiRoutingRepository>(new FakeRepository([],[],ProjectAiRoutingPolicy.Create(project))).BuildServiceProvider();var result=await services.GetRequiredService<IModelRouter>().SelectAsync(new(project,null,AgentRole.Planner,"Plan this",Guid.NewGuid()),default);Assert.False(result.Succeeded);Assert.Equal("invalid_override",result.FailureCode);
    }
    [Fact] public async Task Project_readiness_is_scoped_and_uses_router_eligibility()
    {
        var project=Project.Create("Project",null,"https://github.com/example/repo","main");var connection=AiProviderConnection.Create(ProviderType.OpenAI,"OpenAI");connection.Connected();var model=DiscoveredModel.Create(connection.Id,ProviderType.OpenAI,"gpt-test","GPT",null,ModelLifecycleStatus.Stable,CapabilityMetadataSource.VersionedProviderMapping,"7",128000,8192);
        var services=new ServiceCollection().AddApplication().AddSingleton<IProjectRepository>(new FakeProjects([project])).AddSingleton<IAiRoutingRepository>(new FakeRepository([connection],[model],ProjectAiRoutingPolicy.Create(project.Id))).BuildServiceProvider();var service=services.GetRequiredService<IProjectAiService>();
        var readiness=await service.GetReadinessAsync(project.Id,default);Assert.Equal("Ready",readiness!.RoutingStatus);Assert.Equal(1,readiness.DiscoveredEligiblePlannerModels);Assert.Null(await service.GetReadinessAsync(Guid.NewGuid(),default));
    }
    private sealed class FakeRepository(IReadOnlyList<AiProviderConnection> connections,IReadOnlyList<DiscoveredModel> models,ProjectAiRoutingPolicy policy):IAiRoutingRepository
    {
        public Task<IReadOnlyList<AiProviderConnection>> GetConnectionsAsync(CancellationToken ct)=>Task.FromResult(connections);
        public Task<AiProviderConnection?> GetConnectionAsync(Guid id,CancellationToken ct)=>Task.FromResult(connections.FirstOrDefault(x=>x.Id==id));
        public Task<IReadOnlyList<DiscoveredModel>> GetModelsAsync(Guid? id,CancellationToken ct)=>Task.FromResult<IReadOnlyList<DiscoveredModel>>(models.Where(x=>id is null||x.ProviderConnectionId==id).ToList());
        public Task<ProjectAiRoutingPolicy?> GetPolicyAsync(Guid id,CancellationToken ct)=>Task.FromResult<ProjectAiRoutingPolicy?>(policy);
        public Task<ProjectAiRoutingPolicy> GetOrCreatePolicyAsync(Guid id,CancellationToken ct)=>Task.FromResult(policy);
        public Task<ModelSelectionDecision?> GetDecisionAsync(Guid project,Guid run,CancellationToken ct)=>Task.FromResult<ModelSelectionDecision?>(null);
        public Task AddConnectionAsync(AiProviderConnection x,CancellationToken ct)=>Task.CompletedTask;public Task AddModelAsync(DiscoveredModel x,CancellationToken ct)=>Task.CompletedTask;public Task RemoveConnectionAsync(AiProviderConnection x,CancellationToken ct)=>Task.CompletedTask;public Task AddDecisionAsync(ModelSelectionDecision x,CancellationToken ct)=>Task.CompletedTask;public Task SaveChangesAsync(CancellationToken ct)=>Task.CompletedTask;
    }
    private sealed class FakeProjects(IReadOnlyList<Project> projects):IProjectRepository
    {
        public Task AddAsync(Project project,CancellationToken ct)=>Task.CompletedTask;public Task<Project?> GetAsync(Guid id,CancellationToken ct)=>Task.FromResult(projects.FirstOrDefault(x=>x.Id==id));public Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status,string? search,CancellationToken ct)=>Task.FromResult(projects);public Task SaveChangesAsync(CancellationToken ct)=>Task.CompletedTask;
    }
}
