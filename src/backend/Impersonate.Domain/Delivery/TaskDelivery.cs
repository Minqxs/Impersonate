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
    public string? DeliveryBaseCommitSha
    {
        get; private set;
    }
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
    public string? RemoteName
    {
        get; private set;
    }
    public string? RemoteRepository
    {
        get; private set;
    }
    public string? RemoteBranchName
    {
        get; private set;
    }
    public string? PushedCommitSha
    {
        get; private set;
    }
    public DateTimeOffset? PushedAtUtc
    {
        get; private set;
    }
    public string ValidationSummaryJson { get; private set; } = "[]";
    public Guid? ClaimId
    {
        get; private set;
    }
    public DateTimeOffset? ClaimedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset? ClaimExpiresAtUtc
    {
        get; private set;
    }
    public string? ClaimOwner
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
    public string? PullRequestHeadBranch
    {
        get; private set;
    }
    public string? PullRequestBaseBranch
    {
        get; private set;
    }
    public string? PullRequestObservedHeadSha
    {
        get; private set;
    }
    public DateTimeOffset? PullRequestCreatedAtUtc
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
    public int DeliveryReviewAttemptCount
    {
        get; private set;
    }
    public int DeliveryRepairAttemptCount
    {
        get; private set;
    }
    public bool IsActive => Status is not (TaskDeliveryStatus.MergedIntoRun or TaskDeliveryStatus.Failed or TaskDeliveryStatus.Blocked or TaskDeliveryStatus.Cancelled);

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
    public void Claim(Guid claimId, string owner, DateTimeOffset expiresAt, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        if (!IsActive)
            throw Invalid("Delivery cannot be claimed in its current state.");
        if (ClaimExpiresAtUtc > now)
            throw Invalid("Delivery already has an active claim.");
        if (claimId == Guid.Empty || expiresAt <= now)
            throw new ArgumentException("A valid delivery claim is required.");
        ClaimId = claimId;
        ClaimOwner = Required(owner, 200);
        ClaimedAtUtc = now;
        ClaimExpiresAtUtc = expiresAt;
        UpdatedAtUtc = now;
    }
    public void ReleaseClaim()
    {
        ClaimId = null;
        ClaimOwner = null;
        ClaimedAtUtc = null;
        ClaimExpiresAtUtc = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
    public void RecordDeliveryBase(string sha, DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.Preparing);
        var value = Required(sha, 64);
        if (DeliveryBaseCommitSha is not null && !string.Equals(DeliveryBaseCommitSha, value, StringComparison.OrdinalIgnoreCase))
            throw Invalid("Delivery base conflicts with the persisted identity.");
        DeliveryBaseCommitSha = value;
        UpdatedAtUtc = at ?? DateTimeOffset.UtcNow;
    }
    public void RecordBranchIntent(string branchName, DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.Preparing);
        var value = Required(branchName, 250);
        if (BranchName is not null && !string.Equals(BranchName, value, StringComparison.Ordinal))
            throw Invalid("Branch name conflicts with the persisted identity.");
        BranchName = value;
        UpdatedAtUtc = at ?? DateTimeOffset.UtcNow;
    }
    public void RecordBranchPrepared(string branchName, DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.Preparing);
        if (string.IsNullOrWhiteSpace(DeliveryBaseCommitSha))
            throw Invalid("Delivery base must be resolved before preparing a branch.");
        var value = Required(branchName, 250);
        if (BranchName is not null && !string.Equals(BranchName, value, StringComparison.Ordinal))
            throw Invalid("Branch name conflicts with the persisted identity.");
        BranchName = value;
        Set(TaskDeliveryStatus.BranchPrepared, at);
    }
    public void RecordPatchApplied(DateTimeOffset? at = null) => Move(TaskDeliveryStatus.BranchPrepared, TaskDeliveryStatus.PatchApplied, at);
    public void RecordValidated(string validationSummaryJson = "[]", DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.PatchApplied);
        ValidationSummaryJson = Required(validationSummaryJson, 16000);
        Set(TaskDeliveryStatus.Validated, at);
    }
    public void RecordCommitted(string commitSha, DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.Validated);
        CommitSha = Required(commitSha, 64);
        Set(TaskDeliveryStatus.Committed, at);
    }
    public void RecordPushed(string remoteName, string remoteRepository, string remoteBranchName, string pushedCommitSha, DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.Committed);
        var commit = Required(pushedCommitSha, 64);
        if (!string.Equals(commit, CommitSha, StringComparison.OrdinalIgnoreCase))
            throw Invalid("Pushed commit must match the approved delivery commit.");
        RemoteName = Required(remoteName, 50);
        RemoteRepository = Required(remoteRepository, 300);
        RemoteBranchName = Required(remoteBranchName, 250);
        PushedCommitSha = commit;
        PushedAtUtc = at ?? DateTimeOffset.UtcNow;
        Set(TaskDeliveryStatus.Pushed, PushedAtUtc);
    }
    public TaskDeliveryStatus? RecoveryStatus
    {
        get; private set;
    }
    public void RecordPullRequestOpen(string provider, string repository, long number, string safeUrl, string headBranch, string baseBranch, string observedHeadSha, DateTimeOffset createdAt, DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.Pushed);
        if (number <= 0)
            throw new ArgumentOutOfRangeException(nameof(number));
        PullRequestProvider = Required(provider, 50);
        PullRequestRepository = Required(repository, 300);
        PullRequestNumber = number;
        PullRequestUrl = Required(safeUrl, 1000);
        PullRequestHeadBranch = Required(headBranch, 250);
        PullRequestBaseBranch = Required(baseBranch, 200);
        PullRequestObservedHeadSha = Required(observedHeadSha, 64);
        if (!string.Equals(PullRequestObservedHeadSha, CommitSha, StringComparison.OrdinalIgnoreCase))
            throw Invalid("Pull-request head must match the approved commit.");
        PullRequestCreatedAtUtc = createdAt;
        Set(TaskDeliveryStatus.PullRequestOpen, at);
    }
    public void StartDeliveryReview(DateTimeOffset? at = null) => Move(TaskDeliveryStatus.PullRequestOpen, TaskDeliveryStatus.DeliveryReview, at);
    public void RecordDeliveryReviewAttempt(DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.DeliveryReview);
        DeliveryReviewAttemptCount++;
        UpdatedAtUtc = at ?? DateTimeOffset.UtcNow;
    }
    public void RequestDeliveryChanges(DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.DeliveryReview);
        DeliveryRepairAttemptCount++;
        Set(TaskDeliveryStatus.ChangesRequested, at);
    }
    public void RecordRepairCommit(string commitSha, string validationSummaryJson, DateTimeOffset? at = null)
    {
        Ensure(TaskDeliveryStatus.ChangesRequested);
        CommitSha = PushedCommitSha = PullRequestObservedHeadSha = Required(commitSha, 64);
        ValidationSummaryJson = Required(validationSummaryJson, 16000);
        PushedAtUtc = at ?? DateTimeOffset.UtcNow;
        Set(TaskDeliveryStatus.DeliveryReview, at);
    }
    public void ApproveForIntegration(DateTimeOffset? at = null) => Move(TaskDeliveryStatus.DeliveryReview, TaskDeliveryStatus.ApprovedForIntegration, at);
    public void RequestMerge(DateTimeOffset? at = null) => Move(TaskDeliveryStatus.ApprovedForIntegration, TaskDeliveryStatus.MergeRequested, at);
    public void MarkMergedIntoRun(DateTimeOffset? at = null)
    {
        if (Status != TaskDeliveryStatus.MergeRequested || PullRequestNumber is null || string.IsNullOrWhiteSpace(PullRequestRepository))
            throw Invalid("Run integration requires an internal pull-request identity.");
        Set(TaskDeliveryStatus.MergedIntoRun, at, true);
    }
    public void Fail(string code, string message, DateTimeOffset? at = null)
    {
        EnsureActive();
        RecoveryStatus = Status;
        FailureCode = Required(code, 100);
        FailureMessage = Required(message, 1000);
        Set(TaskDeliveryStatus.Failed, at, true);
    }
    public void Block(string code, string message, DateTimeOffset? at = null)
    {
        EnsureActive();
        RecoveryStatus = Status;
        FailureCode = Required(code, 100);
        FailureMessage = Required(message, 1000);
        Set(TaskDeliveryStatus.Blocked, at, true);
    }
    public void Recover(DateTimeOffset? at = null)
    {
        if (Status is not (TaskDeliveryStatus.Failed or TaskDeliveryStatus.Blocked))
            throw Invalid("Only failed or blocked delivery can recover.");
        var resume = RecoveryStatus ?? TaskDeliveryStatus.Pending;
        if (resume is TaskDeliveryStatus.MergedIntoRun or TaskDeliveryStatus.Failed or TaskDeliveryStatus.Blocked or TaskDeliveryStatus.Cancelled)
            throw Invalid("Recovery checkpoint is invalid.");
        FailureCode = null;
        FailureMessage = null;
        RecoveryStatus = null;
        CompletedAtUtc = null;
        Set(resume, at);
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
