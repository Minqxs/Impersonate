namespace Impersonate.Domain.Delivery;

public enum TaskDeliveryStatus
{
    Pending, Preparing, BranchPrepared, PatchApplied, Validated, Committed, Pushed,
    PullRequestOpen, AwaitingMerge, Merged, Failed, Blocked, Cancelled
}
