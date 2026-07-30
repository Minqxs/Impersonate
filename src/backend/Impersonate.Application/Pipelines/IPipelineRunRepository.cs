using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public interface IPipelineRunRepository
{
    Task AddAsync(PipelineRun run, CancellationToken ct);
    Task<PipelineRun?> GetAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task<PipelineRun?> ClaimNextExecutionAsync(Guid claimId, string workerId, DateTimeOffset claimedAt, DateTimeOffset expiresAt, CancellationToken ct);
    Task<IReadOnlyList<PlanningAttempt>> GetPlanningAttemptsAsync(Guid runId, CancellationToken ct);
    Task<IReadOnlyList<PipelineRun>> ListAsync(Guid projectId, PipelineRunStatus? status, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);
    Task DeleteAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
