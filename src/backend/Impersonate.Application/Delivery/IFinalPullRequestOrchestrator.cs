namespace Impersonate.Application.Delivery;

public interface IFinalPullRequestOrchestrator
{
    Task<bool> ProcessOneAsync(string workerId, CancellationToken cancellationToken);
}
