using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryRepository
{
    Task<TaskDelivery?> GetByTaskAsync(Guid projectId, Guid runId, Guid taskId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken);
    Task AddAsync(TaskDelivery delivery, CancellationToken cancellationToken);
    Task<TaskDelivery?> ClaimNextPendingAsync(Guid claimId, string owner, DateTimeOffset claimedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task<TaskDelivery?> ClaimNextReconciliationAsync(Guid claimId, string owner, DateTimeOffset claimedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken) => Task.FromResult<TaskDelivery?>(null);
    async Task<TaskDelivery?> RecoverAsync(Guid projectId, Guid runId, Guid deliveryId, string approvedPatchSha256, Guid approvedReviewDecisionId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var delivery = (await ListByRunAsync(projectId, runId, cancellationToken)).SingleOrDefault(x => x.Id == deliveryId && x.ApprovedReviewDecisionId == approvedReviewDecisionId && string.Equals(x.ApprovedPatchSha256, approvedPatchSha256, StringComparison.OrdinalIgnoreCase) && x.Status is TaskDeliveryStatus.Blocked or TaskDeliveryStatus.Failed && !(x.ClaimExpiresAtUtc > at));
        if (delivery is null)
            return null;
        delivery.Recover(at);
        await SaveChangesAsync(cancellationToken);
        return delivery;
    }
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
