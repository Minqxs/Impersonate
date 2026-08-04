using Impersonate.Application.Delivery;
using Impersonate.Domain.Delivery;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfTaskDeliveryReviewRepository(ImpersonateDbContext db) : ITaskDeliveryReviewRepository
{
    public async Task<IReadOnlyList<TaskDeliveryReview>> ListAsync(Guid deliveryId, CancellationToken ct) => await db.TaskDeliveryReviews.Where(x => x.TaskDeliveryId == deliveryId).OrderBy(x => x.ReviewAttemptNumber).ToListAsync(ct);
    public Task AddAsync(TaskDeliveryReview review, CancellationToken ct) => db.TaskDeliveryReviews.AddAsync(review, ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
