namespace Impersonate.Domain.Pipelines;

public sealed class PlannedTask
{
    private readonly List<TaskAttempt> attempts = [];
    private readonly List<ReviewDecision> reviewDecisions = [];
    private PlannedTask()
    {
    }

    internal static PlannedTask Create(Guid runId, int sequence, string title, string description, IReadOnlyList<string> criteria, int max, DateTimeOffset? at)
    {
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        if (max is < 0 or > 20)
            throw new ArgumentOutOfRangeException(nameof(max));
        if (criteria.Count == 0 || criteria.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Acceptance criteria are required.");
        return new()
        {
            Id = Guid.NewGuid(),
            PipelineRunId = runId,
            Sequence = sequence,
            Title = PipelineRun.Required(title, 200),
            Description = PipelineRun.Required(description, 4000),
            AcceptanceCriteriaJson = System.Text.Json.JsonSerializer.Serialize(criteria.Select(x => PipelineRun.Required(x, 500))),
            MaximumRevisionAttempts = max,
            CreatedAtUtc = at ?? DateTimeOffset.UtcNow
        };
    }

    public Guid Id
    {
        get; private set;
    }
    public Guid PipelineRunId
    {
        get; private set;
    }
    public int Sequence
    {
        get; private set;
    }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string AcceptanceCriteriaJson { get; private set; } = "[]";
    public IReadOnlyList<string> AcceptanceCriteria => System.Text.Json.JsonSerializer.Deserialize<List<string>>(AcceptanceCriteriaJson) ?? [];
    public PlannedTaskStatus Status
    {
        get; private set;
    }
    public int RevisionCount
    {
        get; private set;
    }
    public int MaximumRevisionAttempts
    {
        get; private set;
    }
    public Guid? CoderModelOverrideId
    {
        get; private set;
    }
    public Guid? ReviewerModelOverrideId
    {
        get; private set;
    }
    public string DependsOnTaskIdsJson { get; private set; } = "[]";
    public string AffectedAreasJson { get; private set; } = "[]";
    public string ChangeType { get; private set; } = "Unknown";
    public string Risk { get; private set; } = "Unknown";
    public string ConflictRisk { get; private set; } = "Unknown";
    public string? ExecutionReason
    {
        get; private set;
    }
    public string RepositoryEvidenceJson { get; private set; } = "[]";
    public int OriginalPlannerSequence
    {
        get; private set;
    }
    public bool OrderAdjusted
    {
        get; private set;
    }
    public string? OrderAdjustmentReason
    {
        get; private set;
    }
    public bool EstablishesSharedContract
    {
        get; private set;
    }
    public string? SkipReason
    {
        get; private set;
    }
    public string? FailureReason
    {
        get; private set;
    }
    public DateTimeOffset CreatedAtUtc
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
    public IReadOnlyList<TaskAttempt> Attempts => attempts.AsReadOnly();
    public IReadOnlyList<ReviewDecision> ReviewDecisions => reviewDecisions.AsReadOnly();
    public bool IsTerminal => Status is PlannedTaskStatus.Committed or PlannedTaskStatus.Skipped or PlannedTaskStatus.Failed or PlannedTaskStatus.Cancelled;
    public bool IsExecutionResolved => Status is PlannedTaskStatus.Approved or PlannedTaskStatus.Skipped;

    public void SetModelOverrides(Guid? coder, Guid? reviewer)
    {
        if (Status != PlannedTaskStatus.Pending)
            throw PipelineRun.Invalid("Model overrides cannot change after execution starts.");
        CoderModelOverrideId = coder;
        ReviewerModelOverrideId = reviewer;
    }

    internal void ResetForRetry()
    {
        if (Status is not (PlannedTaskStatus.Skipped or PlannedTaskStatus.Failed))
            throw PipelineRun.Invalid("Only failed or skipped tasks can be retried.");
        Status = PlannedTaskStatus.Pending;
        SkipReason = null;
        FailureReason = null;
        CompletedAtUtc = null;
    }

    public void SetIntelligence(IReadOnlyList<Guid> dependencies, IReadOnlyList<string> affectedAreas, string changeType, string risk, string conflictRisk, string executionReason, IReadOnlyList<string> evidence, int originalSequence, bool adjusted, string? adjustmentReason, bool establishesContract)
    {
        if (Status != PlannedTaskStatus.Pending)
            throw PipelineRun.Invalid("Task intelligence is immutable after execution starts.");
        DependsOnTaskIdsJson = System.Text.Json.JsonSerializer.Serialize(dependencies);
        AffectedAreasJson = System.Text.Json.JsonSerializer.Serialize(affectedAreas.Take(30).Select(x => PipelineRun.Required(x, 300)));
        ChangeType = PipelineRun.Required(changeType, 100);
        Risk = PipelineRun.Required(risk, 30);
        ConflictRisk = PipelineRun.Required(conflictRisk, 30);
        ExecutionReason = PipelineRun.Required(executionReason, 1000);
        RepositoryEvidenceJson = System.Text.Json.JsonSerializer.Serialize(evidence.Take(30).Select(x => PipelineRun.Required(x, 500)));
        OriginalPlannerSequence = originalSequence;
        OrderAdjusted = adjusted;
        OrderAdjustmentReason = string.IsNullOrWhiteSpace(adjustmentReason) ? null : PipelineRun.Required(adjustmentReason, 1000);
        EstablishesSharedContract = establishesContract;
    }

    public TaskAttempt StartCoding(DateTimeOffset? at = null)
    {
        if (Status != PlannedTaskStatus.Pending)
            throw PipelineRun.Invalid("Only pending tasks can start coding.");
        Status = PlannedTaskStatus.Coding;
        StartedAtUtc = at ?? DateTimeOffset.UtcNow;
        return AddAttempt(TaskAttemptType.Initial, at);
    }

    public void CompleteAttempt(string summary, DateTimeOffset? at = null)
    {
        var a = attempts.LastOrDefault(x => x.Status == TaskAttemptStatus.Started) ?? throw PipelineRun.Invalid("No active attempt.");
        a.Complete(summary, at);
    }

    public void SubmitForReview()
    {
        if (Status != PlannedTaskStatus.Coding || attempts.LastOrDefault()?.Status != TaskAttemptStatus.Completed)
            throw PipelineRun.Invalid("A completed coding attempt is required before review.");
        Status = PlannedTaskStatus.Reviewing;
    }

    public ReviewDecision Review(ReviewDecisionType decision, string summary, string? feedback = null, DateTimeOffset? at = null)
    {
        if (Status != PlannedTaskStatus.Reviewing)
            throw PipelineRun.Invalid("Task is not awaiting review.");
        var attempt = attempts.Last();
        if (reviewDecisions.Any(x => x.TaskAttemptId == attempt.Id && x.IsCurrent))
            throw PipelineRun.Invalid("The attempt already has a current review.");
        if (decision == ReviewDecisionType.ChangesRequested && string.IsNullOrWhiteSpace(feedback))
            throw new ArgumentException("Feedback is required when changes are requested.");
        foreach (var old in reviewDecisions.Where(x => x.IsCurrent))
            old.Supersede();
        var review = ReviewDecision.Create(Id, attempt.Id, decision, summary, feedback, at);
        reviewDecisions.Add(review);
        Status = decision == ReviewDecisionType.Approved ? PlannedTaskStatus.Approved : PlannedTaskStatus.ChangesRequested;
        return review;
    }

    public TaskAttempt StartRevision(DateTimeOffset? at = null)
    {
        if (Status != PlannedTaskStatus.ChangesRequested)
            throw PipelineRun.Invalid("Changes must be requested first.");
        if (RevisionCount >= MaximumRevisionAttempts)
            throw PipelineRun.Invalid("Retry limit reached.");
        RevisionCount++;
        Status = PlannedTaskStatus.Coding;
        return AddAttempt(TaskAttemptType.Revision, at);
    }

    internal void RollbackExecutionStartForInfrastructure()
    {
        if (Status != PlannedTaskStatus.Coding)
            throw PipelineRun.Invalid("Task is not starting Coder execution.");
        var attempt = attempts.LastOrDefault() ?? throw PipelineRun.Invalid("No task attempt exists.");
        if (attempt.Status != TaskAttemptStatus.Started || attempt.Provider is not null)
            throw PipelineRun.Invalid("Coder execution has already started.");
        attempts.Remove(attempt);
        if (attempt.AttemptType == TaskAttemptType.Revision)
        {
            RevisionCount--;
            Status = PlannedTaskStatus.ChangesRequested;
        }
        else
        {
            Status = PlannedTaskStatus.Pending;
            StartedAtUtc = null;
        }
    }

    public void StartCommit()
    {
        if (Status != PlannedTaskStatus.Approved || reviewDecisions.LastOrDefault()?.Decision != ReviewDecisionType.Approved)
            throw PipelineRun.Invalid("Reviewer approval is required before commit.");
        Status = PlannedTaskStatus.Committing;
    }

    public void MarkCommitted(DateTimeOffset? at = null)
    {
        if (Status != PlannedTaskStatus.Committing)
            throw PipelineRun.Invalid("Commit has not started.");
        Status = PlannedTaskStatus.Committed;
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
    }

    public void Skip(string reason, DateTimeOffset? at = null)
    {
        if (IsTerminal)
            throw PipelineRun.Invalid("Task is already terminal.");
        SkipReason = PipelineRun.Required(reason, 2000);
        Status = PlannedTaskStatus.Skipped;
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
    }

    public void Fail(string reason, DateTimeOffset? at = null)
    {
        if (IsTerminal)
            throw PipelineRun.Invalid("Task is already terminal.");
        FailureReason = PipelineRun.Required(reason, 2000);
        Status = PlannedTaskStatus.Failed;
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
    }

    public void Cancel(DateTimeOffset? at = null)
    {
        if (IsTerminal)
            return;
        attempts.LastOrDefault(x => x.Status == TaskAttemptStatus.Started)?.Cancel(at);
        Status = PlannedTaskStatus.Cancelled;
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
    }

    private TaskAttempt AddAttempt(TaskAttemptType type, DateTimeOffset? at)
    {
        var a = TaskAttempt.Create(Id, attempts.Count + 1, type, at);
        attempts.Add(a);
        return a;
    }
}
