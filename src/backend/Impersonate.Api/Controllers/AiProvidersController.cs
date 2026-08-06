using Impersonate.Api;
using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Microsoft.AspNetCore.Mvc;

namespace Impersonate.Api.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiProvidersController : ControllerBase
{
    [HttpGet("providers")]
    public async Task<IActionResult> ListProviders(
        [FromServices] IAiProviderConnectionService service,
        CancellationToken ct)
    {
        var supportedProviders = Enum.GetValues<ProviderType>()
            .Where(provider => provider is ProviderType.Anthropic
                or ProviderType.OpenAI
                or ProviderType.GoogleGemini
                or ProviderType.OpenRouter);

        return Ok(new { supportedProviders, connections = await service.ListAsync(ct) });
    }

    [HttpGet("usage/models")]
    public async Task<IActionResult> GetModelUsage(
        [FromQuery] int? days,
        [FromServices] IModelUsageService service,
        CancellationToken ct)
    {
        var requestedDays = days ?? 30;
        return Ok(new
        {
            days = Math.Clamp(requestedDays, 1, 365),
            models = await service.GetPlanningUsageAsync(requestedDays, ct)
        });
    }

    [HttpPost("providers/{providerType}/connections")]
    public async Task<IActionResult> CreateConnection(
        ProviderType providerType,
        [FromBody] CreateProviderConnectionRequest request,
        [FromServices] IAiProviderConnectionService service,
        CancellationToken ct)
    {
        try
        {
            var connection = await service.CreateAsync(providerType, request, ct);
            return Created($"/api/ai/provider-connections/{connection.Id}", connection);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError("validation", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError("provider_connection_exists", ex.Message));
        }
        catch (ProviderCredentialStorageException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiError("credential_storage_failed", ex.Message));
        }
    }

    [HttpPut("provider-connections/{connectionId:guid}/credentials")]
    public async Task<IActionResult> ReplaceCredentials(
        Guid connectionId,
        [FromBody] ReplaceProviderCredentialRequest request,
        [FromServices] IAiProviderConnectionService service,
        CancellationToken ct)
    {
        try
        {
            var connection = await service.ReplaceCredentialsAsync(connectionId, request, ct);
            return connection is null
                ? NotFound(new ApiError("not_found", "Provider connection was not found."))
                : Ok(connection);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError("validation", ex.Message));
        }
        catch (ProviderCredentialStorageException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiError("credential_storage_failed", ex.Message));
        }
    }

    [HttpPost("provider-connections/{connectionId:guid}/validate")]
    public async Task<IActionResult> ValidateConnection(
        Guid connectionId,
        [FromServices] IAiProviderConnectionService service,
        CancellationToken ct)
    {
        return await service.ValidateAsync(connectionId, ct) is { } connection
            ? Ok(connection)
            : NotFound();
    }

    [HttpPost("provider-connections/{connectionId:guid}/sync-models")]
    public async Task<IActionResult> SyncModels(
        Guid connectionId,
        [FromServices] IAiProviderConnectionService service,
        CancellationToken ct)
    {
        try
        {
            return await service.SynchroniseAsync(connectionId, ct) is { } connection
                ? Ok(connection)
                : NotFound();
        }
        catch (ProviderCredentialUnavailableException ex)
        {
            return Conflict(new ApiError(ex.Code, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiError("connection_not_ready", ex.Message));
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiError("provider_unavailable", "The provider could not synchronise models."));
        }
    }

    [HttpGet("provider-connections/{connectionId:guid}/models")]
    public async Task<IActionResult> ListModels(
        Guid connectionId,
        [FromServices] IAiProviderConnectionService service,
        CancellationToken ct)
    {
        return await service.ModelsAsync(connectionId, ct) is { } models
            ? Ok(models)
            : NotFound();
    }

    [HttpPut("provider-connections/{connectionId:guid}/disable")]
    public async Task<IActionResult> DisableConnection(
        Guid connectionId,
        [FromServices] IAiProviderConnectionService service,
        CancellationToken ct)
    {
        return await service.DisableAsync(connectionId, ct) ? NoContent() : NotFound();
    }

    [HttpDelete("provider-connections/{connectionId:guid}")]
    public async Task<IActionResult> RemoveConnection(
        Guid connectionId,
        [FromServices] IAiProviderConnectionService service,
        CancellationToken ct)
    {
        return await service.RemoveAsync(connectionId, ct) ? NoContent() : NotFound();
    }
}
