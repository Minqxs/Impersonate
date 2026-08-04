namespace Impersonate.Application.Delivery;

public sealed record RunDeliveryReviewReference(Guid DecisionId, string ExactHeadSha, string Decision, string Summary);
