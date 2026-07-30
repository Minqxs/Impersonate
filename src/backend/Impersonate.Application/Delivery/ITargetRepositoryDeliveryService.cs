namespace Impersonate.Application.Delivery;

public interface ITargetRepositoryDeliveryService
{
    Task<DeliveryOperationResult<TargetRepositoryDeliveryResult>> DeliverApprovedPatchAsync(ApprovedTaskHandoff handoff, CancellationToken cancellationToken);
}
