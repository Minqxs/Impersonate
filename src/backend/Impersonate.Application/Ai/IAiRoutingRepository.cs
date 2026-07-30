using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public interface IAiRoutingRepository
{
    Task<IReadOnlyList<AiProviderConnection>> GetConnectionsAsync(CancellationToken cancellationToken);
    Task<AiProviderConnection?> GetConnectionAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DiscoveredModel>> GetModelsAsync(Guid? connectionId, CancellationToken cancellationToken);
    Task<ProjectAiRoutingPolicy?> GetPolicyAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ModelSelectionDecision?> GetDecisionAsync(Guid projectId, Guid runId, CancellationToken cancellationToken);
    Task AddConnectionAsync(AiProviderConnection connection, CancellationToken cancellationToken);
    Task AddModelAsync(DiscoveredModel model, CancellationToken cancellationToken);
    Task RemoveConnectionAsync(AiProviderConnection connection, CancellationToken cancellationToken);
    Task<ProjectAiRoutingPolicy> GetOrCreatePolicyAsync(Guid projectId, CancellationToken cancellationToken);
    Task AddDecisionAsync(ModelSelectionDecision decision, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
