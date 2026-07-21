using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfAiRoutingRepository(ImpersonateDbContext db) : IAiRoutingRepository
{
    public async Task<IReadOnlyList<AiProviderConnection>> GetConnectionsAsync(CancellationToken ct) => await db.AiProviderConnections.Include(x => x.Models).OrderBy(x => x.ProviderType).ToListAsync(ct);
    public Task<AiProviderConnection?> GetConnectionAsync(Guid id, CancellationToken ct) => db.AiProviderConnections.Include(x => x.Models).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<DiscoveredModel>> GetModelsAsync(Guid? connection, CancellationToken ct) => await db.DiscoveredModels.Where(x => connection == null || x.ProviderConnectionId == connection).OrderBy(x => x.ProviderModelId).ToListAsync(ct);
    public Task<ProjectAiRoutingPolicy?> GetPolicyAsync(Guid project, CancellationToken ct) => db.ProjectAiRoutingPolicies.SingleOrDefaultAsync(x => x.ProjectId == project, ct);
    public Task<ModelSelectionDecision?> GetDecisionAsync(Guid project, Guid run, CancellationToken ct) => db.ModelSelectionDecisions.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(x => x.ProjectId == project && x.PipelineRunId == run, ct);
    public Task AddConnectionAsync(AiProviderConnection connection, CancellationToken ct) { db.AiProviderConnections.Add(connection); return Task.CompletedTask; }
    public Task AddModelAsync(DiscoveredModel model, CancellationToken ct) { db.DiscoveredModels.Add(model); return Task.CompletedTask; }
    public Task RemoveConnectionAsync(AiProviderConnection connection, CancellationToken ct) { db.AiProviderConnections.Remove(connection); return Task.CompletedTask; }
    public async Task<ProjectAiRoutingPolicy> GetOrCreatePolicyAsync(Guid project, CancellationToken ct) { var policy=await db.ProjectAiRoutingPolicies.SingleOrDefaultAsync(x=>x.ProjectId==project,ct); if(policy is null){policy=ProjectAiRoutingPolicy.Create(project);db.ProjectAiRoutingPolicies.Add(policy);}return policy; }
    public Task AddDecisionAsync(ModelSelectionDecision decision, CancellationToken ct) { db.ModelSelectionDecisions.Add(decision); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
