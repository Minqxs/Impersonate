namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryIntegrator
{
    Task<bool> ProcessOneAsync(string workerId, CancellationToken cancellationToken);
}
