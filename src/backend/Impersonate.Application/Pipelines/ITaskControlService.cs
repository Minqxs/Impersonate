namespace Impersonate.Application.Pipelines;

public interface ITaskControlService
{
    Task<PipelineOperationResult<bool>> ExecuteAsync(Guid projectId, Guid runId, Guid taskId, CancellationToken ct);
}
