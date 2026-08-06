using Impersonate.Api;
using Impersonate.Application.Execution;
using Impersonate.Application.Pipelines;
using Microsoft.AspNetCore.Mvc;

namespace Impersonate.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/pipeline-runs/{pipelineRunId:guid}/tasks")]
public sealed class PipelineTasksController : ControllerBase
{
    [HttpPost("{taskId:guid}/execution/start")]
    public async Task<IActionResult> StartExecution(
        Guid projectId,
        Guid pipelineRunId,
        Guid taskId,
        [FromServices] ITaskControlService service,
        CancellationToken ct)
    {
        var result = await service.ExecuteAsync(projectId, pipelineRunId, taskId, ct);
        return ApiResponseMapper.ToActionResult(result, value => Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}", value));
    }

    [HttpPost("{taskId:guid}/execution/retry")]
    public async Task<IActionResult> RetryExecution(
        Guid projectId,
        Guid pipelineRunId,
        Guid taskId,
        [FromServices] ITaskControlService service,
        CancellationToken ct)
    {
        var result = await service.ExecuteAsync(projectId, pipelineRunId, taskId, ct);
        return ApiResponseMapper.ToActionResult(result, value => Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}", value));
    }

    [HttpPut("{taskId:guid}/model-overrides")]
    public async Task<IActionResult> SetModelOverrides(
        Guid projectId,
        Guid pipelineRunId,
        Guid taskId,
        [FromBody] TaskModelOverridesRequest request,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        return ApiResponseMapper.ToActionResult(await service.SetTaskModelOverridesAsync(projectId, pipelineRunId, taskId, request, ct), Ok);
    }

    [HttpGet("{taskId:guid}/attempts")]
    public async Task<IActionResult> GetAttempts(
        Guid projectId,
        Guid pipelineRunId,
        Guid taskId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        var task = (await service.GetAsync(projectId, pipelineRunId, ct))?.Tasks.SingleOrDefault(candidate => candidate.Id == taskId);
        return task is null ? NotFound() : Ok(task.Attempts);
    }

    [HttpGet("{taskId:guid}/reviews")]
    public async Task<IActionResult> GetReviews(
        Guid projectId,
        Guid pipelineRunId,
        Guid taskId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        var task = (await service.GetAsync(projectId, pipelineRunId, ct))?.Tasks.SingleOrDefault(candidate => candidate.Id == taskId);
        return task is null ? NotFound() : Ok(task.Reviews);
    }

    [HttpGet("{taskId:guid}/attempts/{attemptId:guid}/diff")]
    public async Task<IActionResult> GetAttemptDiff(
        Guid projectId,
        Guid pipelineRunId,
        Guid taskId,
        Guid attemptId,
        [FromServices] IPipelineRunService service,
        [FromServices] IExecutionArtifactStore artifacts,
        CancellationToken ct)
    {
        var task = (await service.GetAsync(projectId, pipelineRunId, ct))?.Tasks.SingleOrDefault(candidate => candidate.Id == taskId);
        var attempt = task?.Attempts.SingleOrDefault(candidate => candidate.Id == attemptId);
        if (attempt?.PatchArtifactReference is null)
        {
            return NotFound();
        }

        try
        {
            return Content(await artifacts.ReadTextAsync(attempt.PatchArtifactReference, 200_000, ct), "text/plain; charset=utf-8");
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }
}
