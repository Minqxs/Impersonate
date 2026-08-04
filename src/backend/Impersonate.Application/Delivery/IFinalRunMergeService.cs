using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface IFinalRunMergeService
{
    Task<DeliveryOperationResult<FinalRunMergeReference>> MergeAsync(RunDelivery delivery, CancellationToken ct);
}
