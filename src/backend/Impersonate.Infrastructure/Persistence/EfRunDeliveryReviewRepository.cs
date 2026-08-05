using Impersonate.Application.Delivery;
using Impersonate.Domain.Delivery;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfRunDeliveryReviewRepository(ImpersonateDbContext db) : IRunDeliveryReviewRepository
{
    public async Task<IReadOnlyList<RunDeliveryReview>> ListAsync(Guid id, CancellationToken ct) => await db.RunDeliveryReviews.Where(x => x.RunDeliveryId == id).OrderBy(x => x.AttemptNumber).ToListAsync(ct);
    public Task AddAsync(RunDeliveryReview review, CancellationToken ct) => db.RunDeliveryReviews.AddAsync(review, ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
