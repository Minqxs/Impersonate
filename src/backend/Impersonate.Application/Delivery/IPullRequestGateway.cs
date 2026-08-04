namespace Impersonate.Application.Delivery;

using Impersonate.Domain.Delivery;

public interface IPullRequestGateway
{
    Task<DeliveryOperationResult<PullRequestReference>> OpenAsync(TaskDelivery delivery, ApprovedTaskHandoff handoff, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<PullRequestObservation>> ReadAsync(TaskDelivery delivery, CancellationToken cancellationToken);
    Task<DeliveryOperationResult<PullRequestReviewContext>> ReadReviewContextAsync(TaskDelivery delivery, CancellationToken cancellationToken) =>
        Task.FromResult(DeliveryOperationResult<PullRequestReviewContext>.Fail("delivery_review_not_supported", "Pull-request review context is unavailable."));
}
