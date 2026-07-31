using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryPushService
{
    Task<DeliveryOperationResult<TaskDeliveryPushResult>> PushAsync(TaskDelivery delivery, CancellationToken cancellationToken);
}
