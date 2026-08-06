using Impersonate.Api;
using Impersonate.Application.Projects;
using Impersonate.Domain.Projects;
using Microsoft.AspNetCore.Mvc;

namespace Impersonate.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> List(
        [FromQuery] ProjectStatus? status,
        [FromQuery] string? search,
        [FromServices] IProjectService service,
        CancellationToken ct)
    {
        return Ok(await service.ListAsync(status, search, ct));
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> Get(
        Guid projectId,
        [FromServices] IProjectService service,
        CancellationToken ct)
    {
        return await service.GetAsync(projectId, ct) is { } project ? Ok(project) : NotFound();
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(
        [FromBody] CreateProjectRequest request,
        [FromServices] IProjectService service,
        CancellationToken ct)
    {
        try
        {
            var project = await service.CreateAsync(request, ct);
            return Created($"/api/projects/{project.Id}", project);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "request"] = [ex.Message]
            }));
        }
    }

    [HttpPut("{projectId:guid}")]
    public async Task<IActionResult> Update(
        Guid projectId,
        [FromBody] UpdateProjectRequest request,
        [FromServices] IProjectService service,
        CancellationToken ct)
    {
        try
        {
            return await service.UpdateAsync(projectId, request, ct) is { } project
                ? Ok(project)
                : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "request"] = [ex.Message]
            }));
        }
    }

    [HttpPatch("{projectId:guid}/status")]
    public async Task<IActionResult> ChangeStatus(
        Guid projectId,
        [FromBody] ChangeStatusRequest request,
        [FromServices] IProjectService service,
        CancellationToken ct)
    {
        try
        {
            return await service.ChangeStatusAsync(projectId, request.Status, ct) is { } project
                ? Ok(project)
                : NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [ex.ParamName ?? "status"] = [ex.Message]
            }));
        }
    }

    [HttpGet("{projectId:guid}/health")]
    public async Task<IActionResult> GetHealth(
        Guid projectId,
        [FromServices] IProjectService service,
        CancellationToken ct)
    {
        return await service.GetHealthAsync(projectId, ct) is { } summary
            ? Ok(summary)
            : NotFound();
    }
}
