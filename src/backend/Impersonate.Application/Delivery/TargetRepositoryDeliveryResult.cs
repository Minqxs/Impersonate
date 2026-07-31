namespace Impersonate.Application.Delivery;

public sealed record TargetRepositoryDeliveryResult(string BranchName, string DeliveryBaseCommitSha, string CommitSha, IReadOnlyList<DeliveryValidationStep> Validation);
