namespace Impersonate.Domain.Pipelines;

public sealed class TaskAttempt
{
    private TaskAttempt()
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
    public int AttemptNumber
    {
        get; private set;
    }
    public TaskAttemptType AttemptType
    {
        get; private set;
    }
    public TaskAttemptStatus Status
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
    public int ToolStepCount
    {
        get; private set;
    }
    public string? Summary
    {
        get; private set;
    }
    public string? FailureCode
    {
        get; private set;
    }
    public string? FailureReason
    {
        get; private set;
    }
    public string ChangedFilesJson { get; private set; } = "[]";
    public string? PatchArtifactReference
    {
        get; private set;
    }
    public string? PatchSha256
    {
        get; private set;
    }
    public string ValidationSummaryJson { get; private set; } = "[]";
    public string? SourceBaseCommitSha
    {
        get; private set;
    }
    public int DependencyPatchCount
    {
        get; private set;
    }
    public string DependencyTaskIdsJson { get; private set; } = "[]";
    public string? ComposedTreeFingerprint
    {
        get; private set;
    }
    public bool CurrentRevisionPatchApplied
    {
        get; private set;
    }
    public string? CompositionStatus
    {
        get; private set;
    }
    public int IncrementalPatchFileCount
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

    public bool IsUnstartedTransientAttempt =>
        Status == TaskAttemptStatus.Started &&
        Provider is null &&
        Model is null &&
        PromptVersion is null &&
        ProviderRequestId is null &&
        InputTokenCount is null &&
        OutputTokenCount is null &&
        ToolStepCount == 0 &&
        Summary is null &&
        FailureCode is null &&
        FailureReason is null &&
        PatchArtifactReference is null &&
        PatchSha256 is null &&
        SourceBaseCommitSha is null &&
        CompositionStatus is null;

    internal static TaskAttempt Create(Guid id, int number, TaskAttemptType type, DateTimeOffset? at) => new()
    {
        Id = Guid.NewGuid(),
        PlannedTaskId = id,
        AttemptNumber = number,
        AttemptType = type,
        Status = TaskAttemptStatus.Started,
        StartedAtUtc = at ?? DateTimeOffset.UtcNow
    };
    public void RecordComposition(string sourceSha, IReadOnlyList<Guid> dependencyTaskIds, string tree, bool revisionApplied)
    {
        if (Status != TaskAttemptStatus.Started)
            throw PipelineRun.Invalid("Attempt is terminal.");
        SourceBaseCommitSha = PipelineRun.Required(sourceSha, 64);
        DependencyPatchCount = dependencyTaskIds.Count;
        DependencyTaskIdsJson = System.Text.Json.JsonSerializer.Serialize(dependencyTaskIds);
        ComposedTreeFingerprint = PipelineRun.Required(tree, 64);
        CurrentRevisionPatchApplied = revisionApplied;
        CompositionStatus = "Composed";
    }

    public void RecordExecution(string provider, string model, string promptVersion, string? requestId, int? input, int? output, int toolSteps, string changedFilesJson, string patchReference, string patchSha256, string validationJson)
    {
        if (Status != TaskAttemptStatus.Started)
            throw PipelineRun.Invalid("Attempt is terminal.");
        if (toolSteps < 0)
            throw new ArgumentOutOfRangeException(nameof(toolSteps));
        Provider = PipelineRun.Required(provider, 50);
        Model = PipelineRun.Required(model, 300);
        PromptVersion = PipelineRun.Required(promptVersion, 50);
        ProviderRequestId = requestId?.Trim();
        InputTokenCount = input;
        OutputTokenCount = output;
        ToolStepCount = toolSteps;
        ChangedFilesJson = PipelineRun.Required(changedFilesJson, 16000);
        IncrementalPatchFileCount = (System.Text.Json.JsonSerializer.Deserialize<List<string>>(changedFilesJson) ?? []).Count;
        PatchArtifactReference = PipelineRun.Required(patchReference, 500);
        PatchSha256 = PipelineRun.Required(patchSha256, 64);
        ValidationSummaryJson = PipelineRun.Required(validationJson, 16000);
    }

    internal void Complete(string summary, DateTimeOffset? at)
    {
        if (Status != TaskAttemptStatus.Started)
            throw PipelineRun.Invalid("Attempt is terminal.");
        Summary = PipelineRun.Required(summary, 2000);
        Status = TaskAttemptStatus.Completed;
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
    }

    public void Fail(string code, string reason, DateTimeOffset? at = null)
    {
        if (Status != TaskAttemptStatus.Started)
            throw PipelineRun.Invalid("Attempt is terminal.");
        FailureCode = PipelineRun.Required(code, 100);
        FailureReason = PipelineRun.Required(reason, 2000);
        Status = TaskAttemptStatus.Failed;
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
    }

    internal void Cancel(DateTimeOffset? at)
    {
        if (Status == TaskAttemptStatus.Started)
        {
            Status = TaskAttemptStatus.Cancelled;
            CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
        }
    }
}
