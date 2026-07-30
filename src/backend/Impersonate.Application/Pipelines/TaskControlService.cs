namespace Impersonate.Application.Pipelines;

public sealed class TaskControlService(IPipelineRunRepository runs) : ITaskControlService
{
    public async Task<PipelineOperationResult<bool>> ExecuteAsync(Guid projectId, Guid runId, Guid taskId, CancellationToken ct)
    {
        var run = await runs.GetAsync(projectId, runId, ct);
        if (run is null)
            return PipelineOperationResult<bool>.Fail("not_found", "Pipeline run was not found.");
        var task = run.Tasks.SingleOrDefault(x => x.Id == taskId);
        if (task is null)
            return PipelineOperationResult<bool>.Fail("not_found", "Task was not found.");
        try
        {
            run.StartTaskExecution(task);
            await runs.SaveChangesAsync(ct);
            return PipelineOperationResult<bool>.Ok(true);
        }
        catch (InvalidOperationException ex)
        {
            return PipelineOperationResult<bool>.Fail("conflict", ex.Message);
        }
    }
}
