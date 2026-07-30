namespace Impersonate.Domain.Pipelines;

public sealed record InfrastructureAttemptRollback(
    Guid PlannedTaskId,
    int TaskSequence,
    TaskAttempt TransientAttempt)
{
    public Guid AttemptId => TransientAttempt.Id;
    public int AttemptNumber => TransientAttempt.AttemptNumber;
    public TaskAttemptType AttemptType => TransientAttempt.AttemptType;
}
