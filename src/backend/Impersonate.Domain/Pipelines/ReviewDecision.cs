namespace Impersonate.Domain.Pipelines;

public sealed class ReviewDecision
{
    private ReviewDecision()
    {
    }

    public Guid Id
    {
        get; private set;
    }
    public Guid PlannedTaskId
    {
        get; private set;
    }
    public Guid TaskAttemptId
    {
        get; private set;
    }
    public ReviewDecisionType Decision
    {
        get; private set;
    }
    public string? Provider
    {
        get; private set;
    }
    public string? Model
    {
        get; private set;
    }
    public string? PromptVersion
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
    public string? ReviewedPatchSha256
    {
        get; private set;
    }
    public string FindingsJson { get; private set; } = "[]";
    public string Summary { get; private set; } = null!;
    public string? Feedback
    {
        get; private set;
    }
    public DateTimeOffset CreatedAtUtc
    {
        get; private set;
    }
    public bool IsCurrent
    {
        get; private set;
    }

    internal static ReviewDecision Create(Guid task, Guid attempt, ReviewDecisionType decision, string summary, string? feedback, DateTimeOffset? at) => new()
    {
        Id = Guid.NewGuid(),
        PlannedTaskId = task,
        TaskAttemptId = attempt,
        Decision = decision,
        Summary = PipelineRun.Required(summary, 2000),
        Feedback = string.IsNullOrWhiteSpace(feedback) ? null : PipelineRun.Required(feedback, 4000),
        CreatedAtUtc = at ?? DateTimeOffset.UtcNow,
        IsCurrent = true
    };
    public void RecordExecution(string provider, string model, string promptVersion, string? requestId, int? input, int? output, string patchSha256, string findingsJson)
    {
        Provider = PipelineRun.Required(provider, 50);
        Model = PipelineRun.Required(model, 300);
        PromptVersion = PipelineRun.Required(promptVersion, 50);
        ProviderRequestId = requestId?.Trim();
        InputTokenCount = input;
        OutputTokenCount = output;
        ReviewedPatchSha256 = PipelineRun.Required(patchSha256, 64);
        FindingsJson = PipelineRun.Required(findingsJson, 16000);
    }

    internal void Supersede() => IsCurrent = false;
}
