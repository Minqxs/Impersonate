using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface IFinalPullRequestGateway
{
    Task<DeliveryOperationResult<FinalPullRequestReference>> OpenAsync(RunDelivery delivery, string title, string body, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<FinalPullRequestObservation>> ReadAsync(RunDelivery delivery, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<FinalRunMergeReference>> MergeAsync(RunDelivery delivery, CancellationToken cancellationToken);
}
