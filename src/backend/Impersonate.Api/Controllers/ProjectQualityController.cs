using Impersonate.Api;
using Impersonate.Application.Quality;
using Microsoft.AspNetCore.Mvc;

namespace Impersonate.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/quality")]
public sealed class ProjectQualityController : ControllerBase
{
    [HttpGet("configuration")]
    public async Task<IActionResult> GetConfiguration(
        Guid projectId,
        [FromServices] IProjectQualityService service,
        CancellationToken ct)
    {
        return await service.GetConfigurationAsync(projectId, ct) is { } configuration
            ? Ok(configuration)
            : NotFound(new ApiError("not_found", "Project was not found."));
    }

    [HttpPut("configuration")]
    public async Task<IActionResult> SaveConfiguration(
        Guid projectId,
        [FromBody] SaveProjectQualityConfigurationRequest request,
        [FromServices] IProjectQualityService service,
        CancellationToken ct)
    {
        try
        {
            return await service.SaveAsync(projectId, request, ct) is { } configuration
                ? Ok(configuration)
                : NotFound(new ApiError("not_found", "Project was not found."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError("quality_configuration_invalid", ex.Message));
        }
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(
        Guid projectId,
        [FromServices] IProjectQualityService service,
        CancellationToken ct)
    {
        return await service.ValidateAsync(projectId, ct) is { } summary
            ? Ok(summary)
            : NotFound(new ApiError("not_found", "Project was not found."));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        Guid projectId,
        [FromServices] IProjectQualityService service,
        CancellationToken ct)
    {
        return await service.GetSummaryAsync(projectId, false, ct) is { } summary
            ? Ok(summary)
            : NotFound(new ApiError("not_found", "Project was not found."));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        Guid projectId,
        [FromServices] IProjectQualityService service,
        CancellationToken ct)
    {
        return await service.GetSummaryAsync(projectId, true, ct) is { } summary
            ? Ok(summary)
            : NotFound(new ApiError("not_found", "Project was not found."));
    }

    [HttpDelete("configuration")]
    public async Task<IActionResult> RemoveConfiguration(
        Guid projectId,
        [FromServices] IProjectQualityService service,
        CancellationToken ct)
    {
        return await service.RemoveAsync(projectId, ct)
            ? NoContent()
            : NotFound(new ApiError("quality_not_configured", "Code quality is not configured."));
    }
}
