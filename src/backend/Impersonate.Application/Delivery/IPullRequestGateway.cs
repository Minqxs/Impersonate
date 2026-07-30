namespace Impersonate.Application.Delivery;

public interface IPullRequestGateway
{
    Task<DeliveryOperationResult<PullRequestReference>> OpenAsync(Guid projectId, Guid deliveryId, string branchName, string commitSha, CancellationToken cancellationToken);
}
