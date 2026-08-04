using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryRecoveryService
{
    Task<DeliveryOperationResult<TaskDelivery>> RetryAsync(Guid projectId, Guid runId, Guid deliveryId, CancellationToken cancellationToken);
}
