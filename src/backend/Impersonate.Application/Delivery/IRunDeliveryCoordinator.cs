using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface IRunDeliveryCoordinator
{
    Task<DeliveryOperationResult<RunDelivery>> GetOrCreateAsync(Guid projectId, Guid runId, CancellationToken ct);
}
