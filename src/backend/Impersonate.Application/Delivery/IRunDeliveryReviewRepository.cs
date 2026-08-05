using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface IRunDeliveryReviewRepository
{
    Task<IReadOnlyList<RunDeliveryReview>> ListAsync(Guid runDeliveryId, CancellationToken cancellationToken);
    Task AddAsync(RunDeliveryReview review, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
