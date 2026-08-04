namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryRepairer
{
    Task<bool> ProcessOneAsync(string workerId, CancellationToken cancellationToken);
}
