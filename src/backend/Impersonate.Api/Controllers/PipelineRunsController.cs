using Impersonate.Api;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Domain.Pipelines;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Impersonate.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/pipeline-runs")]
public sealed class PipelineRunsController : ControllerBase
{
    [HttpPost("")]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreatePipelineRunRequest request,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        var result = await service.CreateAsync(projectId, request, ct);
        return ApiResponseMapper.ToActionResult(result, run => Created($"/api/projects/{projectId}/pipeline-runs/{run.Id}", run));
    }

    [HttpGet("")]
    public async Task<IActionResult> List(
        Guid projectId,
        [FromQuery] PipelineRunStatus? status,
        [FromQuery] DateTimeOffset? createdFrom,
        [FromQuery] DateTimeOffset? createdTo,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        return Ok(await service.ListAsync(projectId, status, createdFrom, createdTo, ct));
    }

    [HttpGet("{pipelineRunId:guid}")]
    public async Task<IActionResult> Get(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        return await service.GetAsync(projectId, pipelineRunId, ct) is { } run
            ? Ok(run)
            : NotFound();
    }

    [HttpGet("{pipelineRunId:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        return await service.TimelineAsync(projectId, pipelineRunId, ct) is { } timeline
            ? Ok(timeline)
            : NotFound();
    }

    [HttpPost("{pipelineRunId:guid}/planning/start")]
    public async Task<IActionResult> StartPlanning(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IPlannerReadiness legacyReadiness,
        [FromServices] IEnumerable<IPipelineRunRepository> repositories,
        [FromServices] IServiceProvider services,
        CancellationToken ct)
    {
        var readiness = legacyReadiness.Get();
        if (!repositories.Any() && !readiness.IsReady)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiError("planner_configuration_unavailable", readiness.Message));
        }

        var service = services.GetRequiredService<IPipelineRunService>();
        var result = await service.StartPlanningAsync(projectId, pipelineRunId, ct);
        return ApiResponseMapper.ToActionResult(result, run => Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}/planning", run));
    }

    [HttpGet("{pipelineRunId:guid}/planning")]
    public async Task<IActionResult> GetPlanning(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        return await service.GetAsync(projectId, pipelineRunId, ct) is { } run
            ? Ok(run)
            : NotFound();
    }

    [HttpGet("{pipelineRunId:guid}/execution/readiness")]
    public async Task<IActionResult> GetExecutionReadiness(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        return ApiResponseMapper.ToActionResult(await service.ExecutionReadinessAsync(projectId, pipelineRunId, ct), Ok);
    }

    [HttpGet("{pipelineRunId:guid}/intelligence")]
    public async Task<IActionResult> GetIntelligence(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        return ApiResponseMapper.ToActionResult(await service.IntelligenceAsync(projectId, pipelineRunId, ct), Ok);
    }

    [HttpPost("{pipelineRunId:guid}/execution/start")]
    public async Task<IActionResult> StartExecution(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        var result = await service.StartExecutionAsync(projectId, pipelineRunId, ct);
        return ApiResponseMapper.ToActionResult(result, run => Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}", run));
    }

    [HttpPost("{pipelineRunId:guid}/execution/retry")]
    public async Task<IActionResult> RetryExecution(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        var result = await service.RetryExecutionAsync(projectId, pipelineRunId, ct);
        return ApiResponseMapper.ToActionResult(result, run => Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}", run));
    }

    [HttpPost("{pipelineRunId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        return ApiResponseMapper.ToActionResult(await service.CancelAsync(projectId, pipelineRunId, ct), Ok);
    }

    [HttpDelete("{pipelineRunId:guid}")]
    public async Task<IActionResult> Delete(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IPipelineRunService service,
        CancellationToken ct)
    {
        return ApiResponseMapper.ToActionResult(await service.DeleteAsync(projectId, pipelineRunId, ct), _ => NoContent());
    }
}
