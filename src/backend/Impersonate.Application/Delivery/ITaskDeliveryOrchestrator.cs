namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryOrchestrator
{
    Task<bool> ProcessOneAsync(string workerId, CancellationToken cancellationToken);
}
