using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public interface IPipelineRunService
{
    Task<PipelineOperationResult<PipelineRunDto>> CreateAsync(Guid projectId, CreatePipelineRunRequest request, CancellationToken ct);
    Task<PipelineRunDto?> GetAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task<IReadOnlyList<PipelineRunDto>> ListAsync(Guid projectId, PipelineRunStatus? status, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);
    Task<IReadOnlyList<PipelineEventDto>?> TimelineAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task<PipelineOperationResult<PipelineRunDto>> StartPlanningAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task<PipelineOperationResult<ExecutionReadinessDto>> ExecutionReadinessAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task<PipelineOperationResult<PipelineIntelligenceDto>> IntelligenceAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task<PipelineOperationResult<PipelineRunDto>> StartExecutionAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task<PipelineOperationResult<PipelineRunDto>> RetryExecutionAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task<PipelineOperationResult<PipelineRunDto>> SetTaskModelOverridesAsync(Guid projectId, Guid runId, Guid taskId, TaskModelOverridesRequest request, CancellationToken ct);
    Task<PipelineOperationResult<PipelineRunDto>> CancelAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task<PipelineOperationResult<bool>> DeleteAsync(Guid projectId, Guid runId, CancellationToken ct);
}
