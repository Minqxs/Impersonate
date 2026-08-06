using Impersonate.Application.Delivery;
using Impersonate.Application.Pipelines;
using Microsoft.AspNetCore.Mvc;

namespace Impersonate.Api;

public static class ApiResponseMapper
{
    public static IActionResult ToActionResult<T>(
        PipelineOperationResult<T> result,
        Func<T, IActionResult> success)
    {
        return result.Succeeded
            ? success(result.Value!)
            : result.Code switch
            {
                "not_found" => new NotFoundObjectResult(new ApiError(result.Code, result.Error!)),
                "invalid_transition" or "project_off" or "conflict" or "execution_not_ready"
                    => new ConflictObjectResult(new ApiError(result.Code, result.Error!)),
                _ => new BadRequestObjectResult(new ApiError(result.Code ?? "validation", result.Error!))
            };
    }

    public static IActionResult ToActionResult<T>(
        DeliveryOperationResult<T> result,
        Func<T, IActionResult> success)
    {
        return result.Succeeded
            ? success(result.Value!)
            : result.Code switch
            {
                "delivery_not_found" => new NotFoundObjectResult(new ApiError(result.Code, result.Error!)),
                "delivery_retry_state_invalid"
                or "delivery_retry_claim_active"
                or "delivery_retry_handoff_changed"
                or "delivery_retry_checkpoint_invalid"
                or "delivery_retry_conflict"
                    => new ConflictObjectResult(new ApiError(result.Code, result.Error!)),
                _ => new BadRequestObjectResult(new ApiError(result.Code ?? "delivery_retry_failed", result.Error!))
            };
    }
}
