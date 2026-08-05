namespace Impersonate.Application.Delivery;

public interface IFinalRunDeliveryService
{
    Task<DeliveryOperationResult<FinalRunMergeReference>> MergeAsync(Guid projectId, Guid runId, CancellationToken cancellationToken);
}
