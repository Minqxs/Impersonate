namespace Impersonate.Application.Delivery;

public interface IFinalRunReviewer
{
    Task<bool> ProcessOneAsync(string workerId, CancellationToken cancellationToken);
}
