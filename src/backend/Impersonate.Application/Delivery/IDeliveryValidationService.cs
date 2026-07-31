namespace Impersonate.Application.Delivery;

public interface IDeliveryValidationService
{
    Task<DeliveryOperationResult<IReadOnlyList<DeliveryValidationStep>>> ValidateAsync(DeliveryWorkspaceReference workspace, CancellationToken cancellationToken);
}
