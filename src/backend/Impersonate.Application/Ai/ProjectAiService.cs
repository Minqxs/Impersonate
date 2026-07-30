using Impersonate.Application.Projects;
using Impersonate.Domain.Ai;
namespace Impersonate.Application.Ai;

internal sealed class ProjectAiService(IProjectRepository projects, IAiRoutingRepository repository, IModelRouter router) : IProjectAiService
{
    public async Task<ProjectAiReadiness?> GetReadinessAsync(Guid projectId, CancellationToken ct)
    {
        if (await projects.GetAsync(projectId, ct) is null)
            return null;
        var connections = await repository.GetConnectionsAsync(ct);
        var connected = connections.Where(x => x.Status == ProviderConnectionStatus.Connected).ToList();
        var selection = await router.SelectAsync(new(projectId, null, AgentRole.Planner, "Evaluate Planner routing readiness for a structured feature plan."), ct);
        var eligible = selection.Succeeded ? 1 + selection.EligibleAlternatives.Count : 0;
        var blockers = selection.Succeeded ? Array.Empty<string>() : new[] { selection.FailureMessage ?? "No eligible Planner model satisfies this project's routing policy." };
        return new(connected.Count, connected.Count, eligible, selection.Succeeded ? "Ready" : "Incomplete", blockers);
    }
    public async Task<ModelSelectionResult?> PreviewAsync(Guid projectId, AgentRole role, string description, Guid? overrideId, CancellationToken ct)
    {
        if (await projects.GetAsync(projectId, ct) is null)
            return null;
        return await router.SelectAsync(new(projectId, null, role, description, overrideId), ct);
    }
}
