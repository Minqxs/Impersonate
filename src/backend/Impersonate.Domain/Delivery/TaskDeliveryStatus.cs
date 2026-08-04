namespace Impersonate.Domain.Delivery;

public enum TaskDeliveryStatus
{
    Pending = 0, Preparing = 1, BranchPrepared = 2, PatchApplied = 3, Validated = 4, Committed = 5, Pushed = 6,
    PullRequestOpen = 7, DeliveryReview = 8, MergedIntoRun = 9, Failed = 10, Blocked = 11, Cancelled = 12,
    ChangesRequested = 13, ConflictResolution = 14, ApprovedForIntegration = 15, MergeRequested = 16
}
