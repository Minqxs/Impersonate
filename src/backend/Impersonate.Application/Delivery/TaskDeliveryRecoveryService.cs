using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

internal sealed class TaskDeliveryRecoveryService(ITaskDeliveryRepository deliveries, ITaskDeliveryCoordinator coordinator) : ITaskDeliveryRecoveryService
{
    public async Task<DeliveryOperationResult<TaskDelivery>> RetryAsync(Guid projectId, Guid runId, Guid deliveryId, CancellationToken ct)
    {
        var delivery = (await deliveries.ListByRunAsync(projectId, runId, ct)).SingleOrDefault(x => x.Id == deliveryId);
        if (delivery is null)
            return DeliveryOperationResult<TaskDelivery>.Fail("delivery_not_found", "Delivery was not found in this project and run.");
        if (delivery.Status is not (TaskDeliveryStatus.Blocked or TaskDeliveryStatus.Failed))
            return DeliveryOperationResult<TaskDelivery>.Fail("delivery_retry_state_invalid", "Only a blocked or failed delivery can be retried.");
        if (delivery.ClaimExpiresAtUtc > DateTimeOffset.UtcNow)
            return DeliveryOperationResult<TaskDelivery>.Fail("delivery_retry_claim_active", "Delivery has an active worker claim.");
        var handoff = await coordinator.BuildHandoffAsync(projectId, runId, delivery.PlannedTaskId, ct);
        if (!handoff.Succeeded || handoff.Value is null || handoff.Value.ApprovedReviewDecisionId != delivery.ApprovedReviewDecisionId || !string.Equals(handoff.Value.ApprovedPatchSha256, delivery.ApprovedPatchSha256, StringComparison.OrdinalIgnoreCase))
            return DeliveryOperationResult<TaskDelivery>.Fail("delivery_retry_handoff_changed", "The approved patch or review identity no longer matches this delivery.");
        try
        {
            var recovered = await deliveries.RecoverAsync(projectId, runId, deliveryId, delivery.ApprovedPatchSha256, delivery.ApprovedReviewDecisionId, DateTimeOffset.UtcNow, ct);
            return recovered is null ? DeliveryOperationResult<TaskDelivery>.Fail("delivery_retry_conflict", "Delivery changed while the retry was being requested.") : DeliveryOperationResult<TaskDelivery>.Ok(recovered);
        }
        catch (InvalidOperationException) { return DeliveryOperationResult<TaskDelivery>.Fail("delivery_retry_checkpoint_invalid", "The persisted delivery recovery checkpoint is invalid."); }
    }
}
