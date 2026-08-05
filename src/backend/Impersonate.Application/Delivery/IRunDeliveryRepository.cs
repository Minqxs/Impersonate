using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface IRunDeliveryRepository
{
    Task<RunDelivery?> GetByRunAsync(Guid projectId, Guid runId, CancellationToken ct);
    Task AddAsync(RunDelivery delivery, CancellationToken ct);
    Task<RunDelivery?> ClaimNextFinalReviewAsync(Guid claimId, string owner, DateTimeOffset at, DateTimeOffset expiresAt, CancellationToken ct) => Task.FromResult<RunDelivery?>(null);
    Task<RunDelivery?> ClaimNextFinalPullRequestAsync(Guid claimId, string owner, DateTimeOffset at, DateTimeOffset expiresAt, CancellationToken ct) => Task.FromResult<RunDelivery?>(null);
    Task SaveChangesAsync(CancellationToken ct);
}
