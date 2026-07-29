namespace Impersonate.Domain.Pipelines;

public sealed class PlanningAttempt
{
    private PlanningAttempt()
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
    public int AttemptNumber
    {
        get; private set;
    }
    public string Provider { get; private set; } = null!; public string Model { get; private set; } = null!; public string PromptVersion { get; private set; } = null!; public PlanningAttemptStatus Status
    {
        get; private set;
    }
    public DateTimeOffset StartedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset? CompletedAtUtc
    {
        get; private set;
    }
    public string? FailureCode
    {
        get; private set;
    }
    public string? FailureMessage
    {
        get; private set;
    }
    public string? ProviderRequestId
    {
        get; private set;
    }
    public int? InputTokenCount
    {
        get; private set;
    }
    public int? OutputTokenCount
    {
        get; private set;
    }
    public static PlanningAttempt Start(Guid runId, int number, string provider, string model, string promptVersion, DateTimeOffset? at = null) => new() { Id = Guid.NewGuid(), PipelineRunId = runId, AttemptNumber = number, Provider = PipelineRun.Required(provider, 50), Model = PipelineRun.Required(model, 200), PromptVersion = PipelineRun.Required(promptVersion, 50), Status = PlanningAttemptStatus.Started, StartedAtUtc = at ?? DateTimeOffset.UtcNow };
    public void Succeed(string? requestId, int? input, int? output, DateTimeOffset? at = null)
    {
        Complete(PlanningAttemptStatus.Succeeded, null, null, requestId, input, output, at);
    }
    public void Fail(PlanningAttemptStatus status, string code, string message, string? requestId = null, DateTimeOffset? at = null)
    {
        if (status is PlanningAttemptStatus.Started or PlanningAttemptStatus.Succeeded)
            throw new ArgumentOutOfRangeException(nameof(status));
        Complete(status, code, message, requestId, null, null, at);
    }
    public void FailWithUsage(PlanningAttemptStatus status, string code, string message, string? requestId, int? input, int? output, DateTimeOffset? at = null)
    {
        if (status is PlanningAttemptStatus.Started or PlanningAttemptStatus.Succeeded)
            throw new ArgumentOutOfRangeException(nameof(status));
        Complete(status, code, message, requestId, input, output, at);
    }
    private void Complete(PlanningAttemptStatus status, string? code, string? message, string? requestId, int? input, int? output, DateTimeOffset? at)
    {
        if (Status != PlanningAttemptStatus.Started)
            throw PipelineRun.Invalid("Planning attempt is terminal.");
        Status = status;
        FailureCode = code is null ? null : PipelineRun.Required(code, 100);
        FailureMessage = message is null ? null : PipelineRun.Required(message, 2000);
        ProviderRequestId = requestId is null ? null : PipelineRun.Required(requestId, 200);
        InputTokenCount = input;
        OutputTokenCount = output;
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
    }
}
