namespace Impersonate.Application.Delivery;

internal sealed class FinalRunDeliveryService(IRunDeliveryRepository deliveries, IFinalRunMergeService merge) : IFinalRunDeliveryService
{
    public async Task<DeliveryOperationResult<FinalRunMergeReference>> MergeAsync(Guid projectId, Guid runId, CancellationToken ct)
    {
        var delivery = await deliveries.GetByRunAsync(projectId, runId, ct);
        return delivery is null ? DeliveryOperationResult<FinalRunMergeReference>.Fail("run_delivery_not_found", "Run delivery was not found.") : await merge.MergeAsync(delivery, ct);
    }
}
