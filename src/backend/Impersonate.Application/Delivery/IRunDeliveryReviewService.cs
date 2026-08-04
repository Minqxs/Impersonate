using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface IRunDeliveryReviewService
{
    Task<DeliveryOperationResult<RunDeliveryReviewReference>> ReviewAsync(RunDelivery delivery, CancellationToken ct);
}
