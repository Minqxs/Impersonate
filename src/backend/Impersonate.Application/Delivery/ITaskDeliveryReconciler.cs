namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryReconciler
{
    Task<bool> ProcessOneAsync(string workerId, CancellationToken cancellationToken);
}
