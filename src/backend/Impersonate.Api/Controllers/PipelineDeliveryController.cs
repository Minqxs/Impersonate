using Impersonate.Api;
using Impersonate.Application.Delivery;
using Microsoft.AspNetCore.Mvc;

namespace Impersonate.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/pipeline-runs/{pipelineRunId:guid}")]
public sealed class PipelineDeliveryController : ControllerBase
{
    [HttpPost("deliveries/{deliveryId:guid}/retry")]
    public async Task<IActionResult> RetryDelivery(
        Guid projectId,
        Guid pipelineRunId,
        Guid deliveryId,
        [FromServices] ITaskDeliveryRecoveryService service,
        CancellationToken ct)
    {
        var result = await service.RetryAsync(projectId, pipelineRunId, deliveryId, ct);
        return ApiResponseMapper.ToActionResult(result, delivery => Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}", delivery));
    }

    [HttpPost("delivery/merge-to-main")]
    public async Task<IActionResult> MergeToMain(
        Guid projectId,
        Guid pipelineRunId,
        [FromServices] IFinalRunDeliveryService service,
        CancellationToken ct)
    {
        return ApiResponseMapper.ToActionResult(await service.MergeAsync(projectId, pipelineRunId, ct), Ok);
    }
}
