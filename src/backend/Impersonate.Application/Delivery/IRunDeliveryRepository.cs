using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface IRunDeliveryRepository
{
    Task<RunDelivery?> GetByRunAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task AddAsync(RunDelivery delivery, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
