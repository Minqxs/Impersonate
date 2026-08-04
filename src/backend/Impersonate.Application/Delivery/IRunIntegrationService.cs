using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface IRunIntegrationService
{
    Task<DeliveryOperationResult<RunIntegrationReference>> PrepareAsync(RunDelivery delivery, CancellationToken ct);
}
