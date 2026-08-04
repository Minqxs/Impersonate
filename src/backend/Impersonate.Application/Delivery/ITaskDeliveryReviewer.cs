namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryReviewer
{
    Task<bool> ProcessOneAsync(string workerId, CancellationToken cancellationToken);
}
