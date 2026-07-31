namespace Impersonate.Application.Delivery;

using Impersonate.Domain.Delivery;

public interface ITargetRepositoryDeliveryService
{
    Task<DeliveryOperationResult<TargetRepositoryDeliveryResult>> DeliverApprovedPatchAsync(TaskDelivery delivery, ApprovedTaskHandoff handoff, CancellationToken cancellationToken);
}
