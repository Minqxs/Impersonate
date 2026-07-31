namespace Impersonate.Application.Delivery;

using Impersonate.Domain.Delivery;

public interface IPullRequestGateway
{
    Task<DeliveryOperationResult<PullRequestReference>> OpenAsync(TaskDelivery delivery, ApprovedTaskHandoff handoff, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<PullRequestObservation>> ReadAsync(TaskDelivery delivery, CancellationToken cancellationToken);
}
