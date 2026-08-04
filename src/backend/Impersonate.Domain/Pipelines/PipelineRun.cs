using Impersonate.Domain.Delivery;

namespace Impersonate.Domain.Pipelines;

public sealed class PipelineRun
{
    public const int FeatureRequestMaxLength = 4000;
    private readonly List<PlannedTask> tasks = [];
    private readonly List<PipelineRunEvent> events = [];
    private readonly List<TaskDelivery> deliveries = [];
    private PipelineRun()
    {
    }

    private PipelineRun(Guid projectId, string request, int maxRevisions, bool continueOnFailure, DateTimeOffset now)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("Project ID is required.", nameof(projectId));
        Id = Guid.NewGuid();
        ProjectId = projectId;
        FeatureRequest = Required(request, FeatureRequestMaxLength);
        CreatedAtUtc = now;
        LoopRun = LoopRun.Create(Id, maxRevisions, continueOnFailure, now);
        AddEvent("PipelineCreated", null, Status.ToString(), "Pipeline run created.", now);
    }

    public Guid Id
    {
        get; private set;
    }
    public Guid ProjectId
    {
        get; private set;
    }
    public string FeatureRequest { get; private set; } = null!;
    public PipelineRunStatus Status { get; private set; } = PipelineRunStatus.Created;
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
    public DateTimeOffset? CancelledAtUtc
    {
        get; private set;
    }
    public string? FailureReason
    {
        get; private set;
    }
    public string? StopReason
    {
        get; private set;
    }

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? InfrastructureFailureCode
    {
        get; private set;
    }

    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string? InfrastructureFailureMessage
    {
        get; private set;
    }
    public Guid? InfrastructureBlockedTaskId
    {
        get; private set;
    }
    public string? PlanningContextArtifactReference
    {
        get; private set;
    }
    public string? PlanningContextSummary
    {
        get; private set;
    }
    public string PlanningLanguagesJson { get; private set; } = "[]";
    public string PlanningFrameworksJson { get; private set; } = "[]";
    public string PlanningWarningsJson { get; private set; } = "[]";
    public Guid? PlanningClaimId
    {
        get; private set;
    }
    public DateTimeOffset? PlanningClaimedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset? PlanningClaimExpiresAtUtc
    {
        get; private set;
    }
    public string? PlanningWorkerId
    {
        get; private set;
    }
    public Guid? ExecutionClaimId
    {
        get; private set;
    }
    public Guid? ExecutionClaimedTaskId
    {
        get; private set;
    }
    public DateTimeOffset? ExecutionClaimedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset? ExecutionClaimExpiresAtUtc
    {
        get; private set;
    }
    public string? ExecutionWorkerId
    {
        get; private set;
    }
    public Guid? TargetExecutionTaskId
    {
        get; private set;
    }
    public LoopRun LoopRun { get; private set; } = null!;
    public IReadOnlyList<PlannedTask> Tasks => tasks.OrderBy(x => x.Sequence).ToList().AsReadOnly();
    public IReadOnlyList<PipelineRunEvent> Events => events.OrderBy(x => x.Sequence).ToList().AsReadOnly();
    public IReadOnlyList<TaskDelivery> Deliveries => deliveries.OrderBy(x => x.TaskSequence).ToList().AsReadOnly();
    public RunDelivery? RunDelivery
    {
        get; private set;
    }

    public static PipelineRun Create(Guid projectId, string request, int maxRevisions = 3, bool continueOnFailure = true, DateTimeOffset? now = null) => new(projectId, request, maxRevisions, continueOnFailure, now ?? DateTimeOffset.UtcNow);
    public void StartPlanning(DateTimeOffset? at = null)
    {
        EnsureActive(PipelineRunStatus.Created);
        Transition(PipelineRunStatus.Planning, "PlanningStarted", "Planning started.", at);
        LoopRun.Start(at);
    }

    public void ClaimPlanning(Guid claimId, string workerId, DateTimeOffset expiresAt, DateTimeOffset? at = null)
    {
        if (Status != PipelineRunStatus.Planning)
            throw Invalid("Pipeline is not planning.");
        var now = at ?? DateTimeOffset.UtcNow;
        if (PlanningClaimExpiresAtUtc > now)
            throw Invalid("Planning is already claimed.");
        PlanningClaimId = claimId;
        PlanningWorkerId = Required(workerId, 200);
        PlanningClaimedAtUtc = now;
        PlanningClaimExpiresAtUtc = expiresAt;
        AddEvent("PlanningClaimed", null, Status.ToString(), "Planning work claimed.", now);
    }

    public void ClearPlanningClaim()
    {
        PlanningClaimId = null;
        PlanningWorkerId = null;
        PlanningClaimedAtUtc = null;
        PlanningClaimExpiresAtUtc = null;
    }

    public void RecordPlanningContext(string artifactReference, string summary, IReadOnlyList<string> languages, IReadOnlyList<string> frameworks)
    {
        EnsureActive(PipelineRunStatus.Planning);
        PlanningContextArtifactReference = Required(artifactReference, 500);
        PlanningContextSummary = Required(summary, 2000);
        PlanningLanguagesJson = System.Text.Json.JsonSerializer.Serialize(languages);
        PlanningFrameworksJson = System.Text.Json.JsonSerializer.Serialize(frameworks);
        AddEvent("PlanningRepositoryContextBuilt", null, Status.ToString(), summary, null);
    }

    public void RecordPlanningWarning(string warning, DateTimeOffset? at = null)
    {
        EnsureActive(PipelineRunStatus.Planning);
        var warnings = System.Text.Json.JsonSerializer.Deserialize<List<string>>(PlanningWarningsJson) ?? [];
        var safe = Required(warning, 1000);
        if (!warnings.Contains(safe, StringComparer.Ordinal))
            warnings.Add(safe);
        PlanningWarningsJson = System.Text.Json.JsonSerializer.Serialize(warnings.Take(10));
        AddEvent("PlanningEvidenceDiscarded", Status.ToString(), Status.ToString(), safe, at);
    }

    public PlannedTask AddTask(int sequence, string title, string description, DateTimeOffset? at = null) => AddTask(sequence, title, description, ["The described work is complete."], at);
    public PlannedTask AddTask(int sequence, string title, string description, IReadOnlyList<string> acceptanceCriteria, DateTimeOffset? at = null)
    {
        if (Status != PipelineRunStatus.Planning)
            throw Invalid("Tasks can only be added while planning.");
        if (tasks.Any(x => x.Sequence == sequence))
            throw Invalid("Task sequence must be unique.");
        var task = PlannedTask.Create(Id, sequence, title, description, acceptanceCriteria, LoopRun.MaximumRevisionAttempts, at);
        tasks.Add(task);
        AddEvent("TaskPlanned", null, task.Status.ToString(), $"Task {sequence} added.", at);
        return task;
    }

    public void MarkReadyForExecution(DateTimeOffset? at = null)
    {
        EnsureActive(PipelineRunStatus.Planning);
        if (tasks.Count == 0)
            throw Invalid("At least one task is required.");
        Transition(PipelineRunStatus.ReadyForExecution, "PlanningCompleted", "Planning completed.", at);
        LoopRun.MoveToCoding();
        ClearPlanningClaim();
    }

    public void RequireClarification(string reason, string question, DateTimeOffset? at = null)
    {
        EnsureActive(PipelineRunStatus.Planning);
        FailureReason = Required(reason, 2000);
        StopReason = Required(question, 1000);
        Transition(PipelineRunStatus.WaitingForClarification, "ClarificationRequired", StopReason, at);
        ClearPlanningClaim();
    }

    public void StartExecution(DateTimeOffset? at = null)
    {
        EnsureActive(PipelineRunStatus.ReadyForExecution);
        if (tasks.Count == 0)
            throw Invalid("At least one task is required.");
        TargetExecutionTaskId = null;
        Transition(PipelineRunStatus.Executing, "ExecutionStarted", "Pipeline execution started.", at);
    }

    public void StartTaskExecution(PlannedTask task, DateTimeOffset? at = null)
    {
        if (!tasks.Contains(task))
            throw Invalid("Task does not belong to this run.");
        if (Status == PipelineRunStatus.Failed)
        {
            if (tasks.Any(x => x.Status is PlannedTaskStatus.Coding or PlannedTaskStatus.Reviewing))
                throw Invalid("Active execution cannot be reopened.");
            FailureReason = null;
            CompletedAtUtc = null;
            LoopRun.Reopen();
        }
        else
            EnsureActive(PipelineRunStatus.ReadyForExecution);
        if (task.Status is PlannedTaskStatus.Skipped or PlannedTaskStatus.Failed)
            task.ResetForRetry();
        else if (task.Status != PlannedTaskStatus.Pending)
            throw Invalid("Only pending, skipped, or failed tasks can be run.");
        var dependencies = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(task.DependsOnTaskIdsJson) ?? [];
        if (dependencies.Select(id => tasks.SingleOrDefault(x => x.Id == id)).Any(x => x?.Status != PlannedTaskStatus.Approved))
            throw Invalid("All task dependencies must be approved before targeted execution.");
        TargetExecutionTaskId = task.Id;
        Transition(PipelineRunStatus.Executing, "TargetedTaskExecutionStarted", $"Task {task.Sequence} queued for individual execution.", at);
    }

    public PlannedTask ClaimNextTask(Guid claimId, string workerId, DateTimeOffset expiresAt, DateTimeOffset? at = null)
    {
        EnsureActive(PipelineRunStatus.Executing);
        var now = at ?? DateTimeOffset.UtcNow;
        if (expiresAt <= now)
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Claim expiry must be in the future.");
        if (ExecutionClaimExpiresAtUtc > now)
            throw Invalid("Task execution is already claimed.");
        var active = tasks.SingleOrDefault(x => x.Status is PlannedTaskStatus.Coding or PlannedTaskStatus.Reviewing);
        var task = active ?? (TargetExecutionTaskId is { } target ? tasks.Single(x => x.Id == target) : tasks.OrderBy(x => x.Sequence).FirstOrDefault(x => !x.IsExecutionResolved)) ?? throw Invalid("No executable task remains.");
        if (TargetExecutionTaskId is null && tasks.Where(x => x.Sequence < task.Sequence).Any(x => !x.IsExecutionResolved))
            throw Invalid("Prior tasks must be resolved first.");
        if (active is null)
        {
            if (task.Status == PlannedTaskStatus.Pending)
                task.StartCoding(now);
            else if (task.Status == PlannedTaskStatus.ChangesRequested)
                task.StartRevision(now);
            else
                throw Invalid("Next task cannot start from its current state.");
        }

        ExecutionClaimId = claimId;
        ExecutionClaimedTaskId = task.Id;
        ExecutionWorkerId = Required(workerId, 200);
        ExecutionClaimedAtUtc = now;
        ExecutionClaimExpiresAtUtc = expiresAt;
        if (task.Status == PlannedTaskStatus.Coding)
            LoopRun.MoveToCoding();
        else
            LoopRun.MoveToReviewing();
        AddEvent(active is null ? "TaskExecutionClaimed" : "TaskExecutionReclaimed", null, task.Status.ToString(), $"Task {task.Sequence} claimed for execution.", now, task.Id);
        return task;
    }

    public void MoveTaskToReview(PlannedTask task, DateTimeOffset? at = null)
    {
        RequireClaimed(task);
        var previous = task.Status.ToString();
        task.SubmitForReview();
        LoopRun.MoveToReviewing();
        AddEvent("TaskReviewStarted", previous, task.Status.ToString(), $"Task {task.Sequence} submitted for review.", at, task.Id);
    }

    public ReviewDecision RecordReview(PlannedTask task, ReviewDecisionType decision, string summary, string? feedback = null, DateTimeOffset? at = null)
    {
        RequireClaimed(task);
        var previous = task.Status.ToString();
        var review = task.Review(decision, summary, feedback, at);
        if (decision == ReviewDecisionType.ChangesRequested)
            LoopRun.MoveToRevising();
        AddEvent(decision == ReviewDecisionType.Approved ? "TaskApproved" : "TaskChangesRequested", previous, task.Status.ToString(), summary, at, task.Id);
        return review;
    }

    public void ResolveRetryExhaustion(PlannedTask task, string reason, DateTimeOffset? at = null)
    {
        RequireClaimed(task);
        if (task.Status != PlannedTaskStatus.ChangesRequested || task.RevisionCount < task.MaximumRevisionAttempts)
            throw Invalid("Task retry limit has not been reached.");
        if (LoopRun.ContinueOnTaskFailure)
        {
            task.Skip(reason, at);
            AddEvent("TaskSkipped", "ChangesRequested", "Skipped", reason, at, task.Id);
            ClearExecutionClaim();
            TryMarkReadyForDelivery(at);
        }
        else
        {
            task.Fail(reason, at);
            AddEvent("TaskFailed", "ChangesRequested", "Failed", reason, at, task.Id);
            ClearExecutionClaim();
            Fail(reason, at);
        }
    }

    public void ResolveExecutionFailure(PlannedTask task, string reason, DateTimeOffset? at = null)
    {
        RequireClaimed(task);
        var previous = task.Status.ToString();
        if (task.Status is not (PlannedTaskStatus.Coding or PlannedTaskStatus.Reviewing))
            throw Invalid("Only active execution can fail.");
        if (TargetExecutionTaskId == task.Id)
        {
            task.Skip(reason, at);
            AddEvent("TaskSkipped", previous, "Skipped", reason, at, task.Id);
            ClearExecutionClaim();
            TargetExecutionTaskId = null;
            Transition(PipelineRunStatus.ReadyForExecution, "TargetedTaskExecutionFailed", $"Task {task.Sequence} can be retried.", at);
            return;
        }

        if (LoopRun.ContinueOnTaskFailure)
        {
            task.Skip(reason, at);
            AddEvent("TaskSkipped", previous, "Skipped", reason, at, task.Id);
            ClearExecutionClaim();
            TryMarkReadyForDelivery(at);
        }
        else
        {
            task.Fail(reason, at);
            AddEvent("TaskFailed", previous, "Failed", reason, at, task.Id);
            ClearExecutionClaim();
            Fail(reason, at);
        }
    }

    public InfrastructureAttemptRollback BlockForInfrastructure(PlannedTask task, string code, string message, DateTimeOffset? at = null)
    {
        RequireClaimed(task);
        if (task.Status != PlannedTaskStatus.Coding)
            throw Invalid("Infrastructure blocking is allowed only before Coder execution.");
        var attempt = task.RollbackExecutionStartForInfrastructure();
        InfrastructureFailureCode = Required(code, 100);
        InfrastructureFailureMessage = Required(message, 1000);
        InfrastructureBlockedTaskId = task.Id;
        ClearExecutionClaim();
        Transition(PipelineRunStatus.WaitingForInfrastructure, "ExecutionInfrastructureBlocked", InfrastructureFailureMessage, at);
        return new InfrastructureAttemptRollback(task.Id, task.Sequence, attempt);
    }

    public void RetryInfrastructure(DateTimeOffset? at = null)
    {
        EnsureActive(PipelineRunStatus.WaitingForInfrastructure);
        if (InfrastructureBlockedTaskId is null)
            throw Invalid("No infrastructure-blocked task is available.");
        InfrastructureFailureCode = null;
        InfrastructureFailureMessage = null;
        InfrastructureBlockedTaskId = null;
        Transition(PipelineRunStatus.Executing, "ExecutionInfrastructureRetry", "Execution resumed after infrastructure recovery.", at);
    }

    public void FinishApprovedTask(PlannedTask task, DateTimeOffset? at = null)
    {
        RequireClaimed(task);
        if (task.Status != PlannedTaskStatus.Approved)
            throw Invalid("Task is not approved.");
        ClearExecutionClaim();
        if (TargetExecutionTaskId == task.Id)
        {
            TargetExecutionTaskId = null;
            if (tasks.Any(x => x.Status is PlannedTaskStatus.Pending or PlannedTaskStatus.Skipped or PlannedTaskStatus.Failed))
            {
                Transition(PipelineRunStatus.ReadyForExecution, "TargetedTaskExecutionCompleted", $"Task {task.Sequence} was approved.", at);
                return;
            }
        }

        TryMarkReadyForDelivery(at);
    }

    public void ClearExecutionClaim()
    {
        ExecutionClaimId = null;
        ExecutionClaimedTaskId = null;
        ExecutionWorkerId = null;
        ExecutionClaimedAtUtc = null;
        ExecutionClaimExpiresAtUtc = null;
    }

    public void TryMarkReadyForDelivery(DateTimeOffset? at = null)
    {
        EnsureActive(PipelineRunStatus.Executing);
        if (tasks.Any(x => !x.IsExecutionResolved))
            return;
        if (!tasks.Any(x => x.Status == PlannedTaskStatus.Approved))
        {
            Fail("Execution ended without an approved task.", at);
            return;
        }

        Transition(PipelineRunStatus.ReadyForDelivery, "ExecutionCompleted", "All executable tasks have been reviewed.", at);
        LoopRun.MoveToCommitting();
        ClearExecutionClaim();
    }

    public void Complete(DateTimeOffset? at = null)
    {
        EnsureActive(PipelineRunStatus.Executing);
        if (tasks.Any(x => !x.IsTerminal))
            throw Invalid("All tasks must be terminal.");
        if (!tasks.Any(x => x.Status == PlannedTaskStatus.Committed))
            throw Invalid("At least one task must be committed.");
        var status = tasks.Any(x => x.Status is PlannedTaskStatus.Skipped or PlannedTaskStatus.Failed) ? PipelineRunStatus.CompletedWithSkippedTasks : PipelineRunStatus.Completed;
        Transition(status, "PipelineCompleted", "Pipeline completed.", at);
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
        LoopRun.Complete(at);
    }

    public void CompleteDelivery(DateTimeOffset? at = null)
    {
        if (Status != PipelineRunStatus.ReadyForDelivery)
            throw Invalid($"Expected {PipelineRunStatus.ReadyForDelivery}; current state is {Status}.");
        var approved = tasks.Where(x => x.Status == PlannedTaskStatus.Approved).ToArray();
        if (approved.Length == 0 || approved.Any(task => deliveries.SingleOrDefault(x => x.PlannedTaskId == task.Id)?.Status != TaskDeliveryStatus.Merged))
            throw Invalid("Every approved task must have one merged delivery.");
        if (deliveries.Any(x => x.Status != TaskDeliveryStatus.Merged))
            throw Invalid("No unresolved delivery may remain.");
        var status = tasks.Any(x => x.Status == PlannedTaskStatus.Skipped) ? PipelineRunStatus.CompletedWithSkippedTasks : PipelineRunStatus.Completed;
        Transition(status, "DeliveryCompleted", "All approved task deliveries were merged.", at);
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
        LoopRun.Complete(at);
    }

    public void Fail(string reason, DateTimeOffset? at = null)
    {
        EnsureNotTerminal();
        FailureReason = Required(reason, 2000);
        Transition(PipelineRunStatus.Failed, "PipelineFailed", FailureReason, at);
        CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
        LoopRun.Fail(reason, at);
    }

    public void Cancel(DateTimeOffset? at = null)
    {
        EnsureNotTerminal();
        foreach (var task in tasks.Where(x => !x.IsTerminal))
            task.Cancel(at);
        ClearPlanningClaim();
        ClearExecutionClaim();
        Transition(PipelineRunStatus.Cancelled, "PipelineCancelled", "Pipeline cancelled.", at);
        CancelledAtUtc = CompletedAtUtc = at ?? DateTimeOffset.UtcNow;
        LoopRun.Cancel(at);
    }

    public void RecordTaskEvent(PlannedTask task, string type, string? previous, string next, string message, DateTimeOffset? at = null) => AddEvent(type, previous, next, message, at, task.Id);
    private void Transition(PipelineRunStatus next, string type, string message, DateTimeOffset? at)
    {
        var previous = Status.ToString();
        Status = next;
        StartedAtUtc ??= next is PipelineRunStatus.Planning or PipelineRunStatus.Executing ? at ?? DateTimeOffset.UtcNow : null;
        AddEvent(type, previous, next.ToString(), message, at);
    }

    private void AddEvent(string type, string? previous, string next, string message, DateTimeOffset? at, Guid? taskId = null) => events.Add(PipelineRunEvent.Create(ProjectId, Id, taskId, type, previous, next, message, events.Count + 1, at));
    private void EnsureActive(PipelineRunStatus expected)
    {
        EnsureNotTerminal();
        if (Status != expected)
            throw Invalid($"Expected {expected}; current state is {Status}.");
    }

    private void RequireClaimed(PlannedTask task)
    {
        if (ExecutionClaimedTaskId != task.Id)
            throw Invalid("Task is not claimed by this execution.");
    }

    private void EnsureNotTerminal()
    {
        if (Status is PipelineRunStatus.ReadyForDelivery or PipelineRunStatus.Completed or PipelineRunStatus.CompletedWithSkippedTasks or PipelineRunStatus.Failed or PipelineRunStatus.Cancelled)
            throw Invalid($"Pipeline is already terminal ({Status}).");
    }

    internal static string Required(string? value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw new ArgumentException("Value is required.");
        if (result.Length > max)
            throw new ArgumentOutOfRangeException(nameof(value), $"Value must not exceed {max} characters.");
        return result;
    }

    internal static InvalidOperationException Invalid(string message) => new(message);
}
