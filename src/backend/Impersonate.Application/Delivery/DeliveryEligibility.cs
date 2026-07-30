namespace Impersonate.Application.Delivery;

public sealed record DeliveryEligibility(Guid PlannedTaskId, bool Eligible, IReadOnlyList<Guid> BlockingDependencyIds);
