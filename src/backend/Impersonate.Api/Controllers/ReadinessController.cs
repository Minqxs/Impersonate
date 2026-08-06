using Impersonate.Application.Execution;
using Impersonate.Application.Planning;
using Impersonate.Infrastructure.Delivery.Mcp;
using Microsoft.AspNetCore.Mvc;

namespace Impersonate.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ReadinessController : ControllerBase
{
    [HttpGet("planner/readiness")]
    public IActionResult GetPlannerReadiness([FromServices] IPlannerReadiness readiness)
    {
        return Ok(readiness.Get());
    }

    [HttpGet("execution/readiness")]
    public async Task<IActionResult> GetExecutionReadiness(
        [FromServices] IExecutionEnvironmentReadinessService readiness,
        CancellationToken ct)
    {
        return Ok(await readiness.CheckAsync(ct));
    }

    [HttpGet("development/preflight")]
    public async Task<IActionResult> GetDevelopmentPreflight(
        [FromQuery] string? targetRepository,
        [FromServices] DevelopmentPreflightService preflight,
        CancellationToken ct)
    {
        return Ok(await preflight.CheckAsync(targetRepository ?? "Minqxs/TaskIt", ct));
    }
}
