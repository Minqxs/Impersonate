namespace Impersonate.Domain.Pipelines;

public enum PlannedTaskStatus
{
    Pending,
    Coding,
    Reviewing,
    ChangesRequested,
    Approved,
    Committing,
    Committed,
    Skipped,
    Failed,
    Cancelled
}
