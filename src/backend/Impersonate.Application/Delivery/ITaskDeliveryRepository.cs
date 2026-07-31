using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryRepository
{
    Task<TaskDelivery?> GetByTaskAsync(Guid projectId, Guid runId, Guid taskId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken);
    Task AddAsync(TaskDelivery delivery, CancellationToken cancellationToken);
    Task<TaskDelivery?> ClaimNextPendingAsync(Guid claimId, string owner, DateTimeOffset claimedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task<TaskDelivery?> ClaimNextReconciliationAsync(Guid claimId, string owner, DateTimeOffset claimedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken) => Task.FromResult<TaskDelivery?>(null);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
