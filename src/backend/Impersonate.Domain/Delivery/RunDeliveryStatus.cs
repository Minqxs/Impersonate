namespace Impersonate.Domain.Delivery;

public enum RunDeliveryStatus
{
    Pending,
    Preparing,
    RunBranchCreated,
    IntegratingTasks,
    AggregateValidation,
    ResolvingConflicts,
    FinalReview,
    ChangesRequested,
    ReadyForFinalPullRequest,
    FinalPullRequestOpen,
    ReadyForMain,
    MergeRequested,
    Merged,
    Blocked,
    Failed,
    Cancelled
}
