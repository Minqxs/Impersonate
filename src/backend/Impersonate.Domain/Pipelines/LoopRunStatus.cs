namespace Impersonate.Domain.Pipelines;

public enum LoopRunStatus
{
    Pending,
    Running,
    WaitingForApproval,
    Completed,
    Failed,
    Cancelled
}
