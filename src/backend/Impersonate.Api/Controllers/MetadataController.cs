using Microsoft.AspNetCore.Mvc;

namespace Impersonate.Api.Controllers;

[ApiController]
[Route("")]
public sealed class MetadataController : ControllerBase
{
    private static readonly object Metadata = new { Name = "Impersonate API", Status = "Running" };

    [HttpGet("")]
    public IActionResult GetRoot() => Ok(Metadata);

    [HttpGet("api/metadata")]
    public IActionResult GetApiMetadata() => Ok(Metadata);
}
