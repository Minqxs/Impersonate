using System.Security.Cryptography;
using System.Text;

namespace Impersonate.Domain.Delivery;

public sealed class TaskDelivery
{
    private TaskDelivery()
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
    public Guid PlannedTaskId
    {
        get; private set;
    }
    public int TaskSequence
    {
        get; private set;
    }
    public string SourceBaseCommitSha { get; private set; } = null!;
    public string ApprovedPatchArtifactReference { get; private set; } = null!;
    public string ApprovedPatchSha256 { get; private set; } = null!;
    public Guid ApprovedReviewDecisionId
    {
        get; private set;
    }
    public string IdempotencyKey { get; private set; } = null!;
    public TaskDeliveryStatus Status
    {
        get; private set;
    }
    public string? BranchName
    {
        get; private set;
    }
    public string? CommitSha
    {
        get; private set;
    }
    public string? PullRequestProvider
    {
        get; private set;
    }
    public string? PullRequestRepository
    {
        get; private set;
    }
    public long? PullRequestNumber
    {
        get; private set;
    }
    public string? PullRequestUrl
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
    public DateTimeOffset CreatedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset UpdatedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset? CompletedAtUtc
    {
        get; private set;
    }
    public bool IsActive => Status is not (TaskDeliveryStatus.Merged or TaskDeliveryStatus.Failed or TaskDeliveryStatus.Blocked or TaskDeliveryStatus.Cancelled);

    public static TaskDelivery Create(Guid projectId, Guid runId, Guid taskId, int sequence, string sourceSha, string patchReference, string patchSha, Guid reviewId, DateTimeOffset? at = null)
    {
        if (projectId == Guid.Empty || runId == Guid.Empty || taskId == Guid.Empty || reviewId == Guid.Empty)
            throw new ArgumentException("Delivery identities are required.");
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        var now = at ?? DateTimeOffset.UtcNow;
        return new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PipelineRunId = runId,
            PlannedTaskId = taskId,
            TaskSequence = sequence,
            SourceBaseCommitSha = Required(sourceSha, 64),
            ApprovedPatchArtifactReference = Required(patchReference, 500),
            ApprovedPatchSha256 = Required(patchSha, 64),
            ApprovedReviewDecisionId = reviewId,
            IdempotencyKey = BuildIdempotencyKey(projectId, runId, taskId, patchSha),
            Status = TaskDeliveryStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public static string BuildIdempotencyKey(Guid projectId, Guid runId, Guid taskId, string patchSha)
    {
        var canonical = $"{projectId:N}:{runId:N}:{taskId:N}:{Required(patchSha, 64).ToLowerInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public void StartPreparing(DateTimeOffset? at = null) => Move(TaskDeliveryStatus.Pending, TaskDeliveryStatus.Preparing, at);
    public void RecordBranchPrepared(string branchName, DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.Preparing);
        BranchName = Required(branchName, 250);
        Set(TaskDeliveryStatus.BranchPrepared, at);
    }
    public void RecordPatchApplied(DateTimeOffset? at = null) => Move(TaskDeliveryStatus.BranchPrepared, TaskDeliveryStatus.PatchApplied, at);
    public void RecordValidated(DateTimeOffset? at = null) => Move(TaskDeliveryStatus.PatchApplied, TaskDeliveryStatus.Validated, at);
    public void RecordCommitted(string commitSha, DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.Validated);
        CommitSha = Required(commitSha, 64);
        Set(TaskDeliveryStatus.Committed, at);
    }
    public void RecordPushed(DateTimeOffset? at = null) => Move(TaskDeliveryStatus.Committed, TaskDeliveryStatus.Pushed, at);
    public void RecordPullRequestOpen(string provider, string repository, long number, string safeUrl, DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.Pushed);
        if (number <= 0)
            throw new ArgumentOutOfRangeException(nameof(number));
        PullRequestProvider = Required(provider, 50);
        PullRequestRepository = Required(repository, 300);
        PullRequestNumber = number;
        PullRequestUrl = Required(safeUrl, 1000);
        Set(TaskDeliveryStatus.PullRequestOpen, at);
    }
    public void AwaitMerge(DateTimeOffset? at = null) => Move(TaskDeliveryStatus.PullRequestOpen, TaskDeliveryStatus.AwaitingMerge, at);
    public void MarkMerged(DateTimeOffset? at = null)
    {
        if (Status is not (TaskDeliveryStatus.PullRequestOpen or TaskDeliveryStatus.AwaitingMerge) || PullRequestNumber is null || string.IsNullOrWhiteSpace(PullRequestRepository))
            throw Invalid("Merged delivery requires an open pull-request identity.");
        Set(TaskDeliveryStatus.Merged, at, true);
    }
    public void Fail(string code, string message, DateTimeOffset? at = null)
    {
        EnsureActive();
        FailureCode = Required(code, 100);
        FailureMessage = Required(message, 1000);
        Set(TaskDeliveryStatus.Failed, at, true);
    }
    public void Block(string code, string message, DateTimeOffset? at = null)
    {
        EnsureActive();
        FailureCode = Required(code, 100);
        FailureMessage = Required(message, 1000);
        Set(TaskDeliveryStatus.Blocked, at, true);
    }
    public void Recover(DateTimeOffset? at = null)
    {
        if (Status is not (TaskDeliveryStatus.Failed or TaskDeliveryStatus.Blocked))
            throw Invalid("Only failed or blocked delivery can recover.");
        FailureCode = null;
        FailureMessage = null;
        CompletedAtUtc = null;
        Set(TaskDeliveryStatus.Pending, at);
    }
    public void Cancel(DateTimeOffset? at = null)
    {
        EnsureActive();
        Set(TaskDeliveryStatus.Cancelled, at, true);
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw Invalid("Only an active delivery can change.");
    }
    private void Move(TaskDeliveryStatus expected, TaskDeliveryStatus next, DateTimeOffset? at)
    {
        Ensure(expected);
        Set(next, at);
    }
    private void Ensure(TaskDeliveryStatus expected)
    {
        if (Status != expected)
            throw Invalid($"Delivery cannot move from {Status}; expected {expected}.");
    }
    private void Set(TaskDeliveryStatus next, DateTimeOffset? at, bool complete = false)
    {
        Status = next;
        UpdatedAtUtc = at ?? DateTimeOffset.UtcNow;
        if (complete)
            CompletedAtUtc = UpdatedAtUtc;
    }
    private static string Required(string value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrWhiteSpace(v))
            throw new ArgumentException("Value is required.");
        if (v.Length > max)
            throw new ArgumentOutOfRangeException(nameof(value));
        return v;
    }
    private static InvalidOperationException Invalid(string message) => new(message);
}
