namespace Impersonate.Domain.Pipelines;

public enum PipelineRunStatus
{
    Created,
    Planning,
    ReadyForExecution,
    WaitingForClarification,
    Executing,
    WaitingForInfrastructure,
    ReadyForDelivery,
    WaitingForApproval,
    Completed,
    CompletedWithSkippedTasks,
    Failed,
    Cancelled
}
