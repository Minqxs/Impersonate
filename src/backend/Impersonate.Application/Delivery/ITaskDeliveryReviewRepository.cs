using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public interface ITaskDeliveryReviewRepository
{
    Task<IReadOnlyList<TaskDeliveryReview>> ListAsync(Guid deliveryId, CancellationToken ct);
    Task AddAsync(TaskDeliveryReview review, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
