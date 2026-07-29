namespace Impersonate.Domain.Pipelines;

public sealed class PipelineRunEvent
{
    private PipelineRunEvent()
    {
    }

    public Guid Id
    {
        get; private set;
    }
    public Guid ProjectId
    {
        get; private set;
    }
    public Guid PipelineRunId
    {
        get; private set;
    }
    public Guid? PlannedTaskId
    {
        get; private set;
    }
    public string EventType { get; private set; } = null!;
    public string? PreviousState
    {
        get; private set;
    }
    public string NewState { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc
    {
        get; private set;
    }
    public int Sequence
    {
        get; private set;
    }

    internal static PipelineRunEvent Create(Guid project, Guid run, Guid? task, string type, string? previous, string next, string message, int sequence, DateTimeOffset? at) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = project,
        PipelineRunId = run,
        PlannedTaskId = task,
        EventType = type,
        PreviousState = previous,
        NewState = next,
        Message = message,
        Sequence = sequence,
        CreatedAtUtc = at ?? DateTimeOffset.UtcNow
    };
}
