namespace Impersonate.Application.Execution;

public sealed class TaskExecutionPersistenceException(
    Guid pipelineRunId,
    Guid plannedTaskId,
    string exceptionType)
    : Exception("Task execution persistence failed.")
{
    public Guid PipelineRunId { get; } = pipelineRunId;
    public Guid PlannedTaskId { get; } = plannedTaskId;
    public string ExceptionType { get; } = exceptionType;
}
