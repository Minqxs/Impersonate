namespace Impersonate.Domain.Pipelines;

public sealed class LoopRun
{
    private LoopRun()
    {
    }

    public Guid Id
    {
        get; private set;
    }
    public Guid PipelineRunId
    {
        get; private set;
    }
    public string LoopDefinitionId { get; private set; } = "feature-delivery";
    public string LoopDefinitionVersion { get; private set; } = "1";
    public LoopRunStatus Status
    {
        get; private set;
    }
    public LoopStage CurrentStage { get; private set; } = LoopStage.Planning;
    public int MaximumRevisionAttempts
    {
        get; private set;
    }
    public bool ContinueOnTaskFailure
    {
        get; private set;
    }
    public int RetryCount
    {
        get; private set;
    }
    public DateTimeOffset? StartedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset? CompletedAtUtc
    {
        get; private set;
    }
    public string? StopReason
    {
        get; private set;
    }
    public string? FailureReason
    {
        get; private set;
    }

    internal static LoopRun Create(Guid run, int max, bool cont, DateTimeOffset at)
    {
        if (max is < 0 or > 20)
            throw new ArgumentOutOfRangeException(nameof(max));
        return new()
        {
            Id = Guid.NewGuid(),
            PipelineRunId = run,
            MaximumRevisionAttempts = max,
            ContinueOnTaskFailure = cont
        };
    }

    internal void Start(DateTimeOffset? at)
    {
        if (Status != LoopRunStatus.Pending)
            throw PipelineRun.Invalid("Loop cannot start.");
        Status = LoopRunStatus.Running;
        StartedAtUtc = at ?? DateTimeOffset.UtcNow;
    }

    internal void Reopen()
    {
        if (Status != LoopRunStatus.Failed)
            throw PipelineRun.Invalid("Only a failed loop can be reopened.");
        Status = LoopRunStatus.Running;
        CurrentStage = LoopStage.Coding;
        FailureReason = null;
        CompletedAtUtc = null;
    }

    internal void MoveToCoding()
    {
        EnsureActive();
        CurrentStage = LoopStage.Coding;
    }

    internal void MoveToReviewing()
    {
        EnsureActive();
        CurrentStage = LoopStage.Reviewing;
    }

    internal void MoveToRevising()
    {
        EnsureActive();
        CurrentStage = LoopStage.Revising;
        RetryCount++;
    }

    internal void MoveToCommitting()
    {
        EnsureActive();
        CurrentStage = LoopStage.Committing;
    }

    internal void Complete(DateTimeOffset? at)
    {
        EnsureActive();
        Status = LoopRunStatus.Completed;
        CurrentStage = LoopStage.Completing;
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
    }

    internal void Fail(string reason, DateTimeOffset? at)
    {
        EnsureActive();
        Status = LoopRunStatus.Failed;
        FailureReason = reason;
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
    }

    internal void Cancel(DateTimeOffset? at)
    {
        EnsureActive();
        Status = LoopRunStatus.Cancelled;
        StopReason = "Cancelled";
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
    }

    private void EnsureActive()
    {
        if (Status is LoopRunStatus.Completed or LoopRunStatus.Failed or LoopRunStatus.Cancelled)
            throw PipelineRun.Invalid("Loop is terminal.");
    }
}
