using Impersonate.Api;
using Impersonate.Application.Ai;
using Microsoft.AspNetCore.Mvc;

namespace Impersonate.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/ai")]
public sealed class ProjectAiController : ControllerBase
{
    [HttpPost("model-selection/preview")]
    public async Task<IActionResult> PreviewModelSelection(
        Guid projectId,
        [FromBody] ModelSelectionPreviewRequest request,
        [FromServices] IProjectAiService service,
        CancellationToken ct)
    {
        return await service.PreviewAsync(projectId, request.Role, request.Description, request.ManualModelOverrideId, ct) is { } result
            ? Ok(result)
            : NotFound(new ApiError("not_found", "Project was not found."));
    }

    [HttpGet("readiness")]
    public async Task<IActionResult> GetReadiness(
        Guid projectId,
        [FromServices] IProjectAiService service,
        CancellationToken ct)
    {
        return await service.GetReadinessAsync(projectId, ct) is { } result
            ? Ok(result)
            : NotFound(new ApiError("not_found", "Project was not found."));
    }
}
