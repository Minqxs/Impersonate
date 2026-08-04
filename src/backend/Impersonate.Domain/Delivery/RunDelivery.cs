namespace Impersonate.Domain.Delivery;

public sealed class RunDelivery
{
    private RunDelivery()
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
    public RunDeliveryStatus Status
    {
        get; private set;
    }
    public string SourceDefaultBranch { get; private set; } = null!;
    public string SourceBaseCommitSha { get; private set; } = null!;
    public string RunBranchName { get; private set; } = null!;
    public string? RunBranchHeadSha
    {
        get; private set;
    }
    public string AggregateValidationSummaryJson { get; private set; } = "[]";
    public Guid? FinalReviewDecisionId
    {
        get; private set;
    }
    public string? FinalReviewedHeadSha
    {
        get; private set;
    }
    public string? FinalPullRequestProvider
    {
        get; private set;
    }
    public string? FinalPullRequestRepository
    {
        get; private set;
    }
    public long? FinalPullRequestNumber
    {
        get; private set;
    }
    public string? FinalPullRequestUrl
    {
        get; private set;
    }
    public string? FinalPullRequestHeadSha
    {
        get; private set;
    }
    public string? FinalPullRequestBaseBranch
    {
        get; private set;
    }
    public string? FinalPullRequestMergeableState
    {
        get; private set;
    }
    public string? RequiredChecksState
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
    public Guid? ClaimId
    {
        get; private set;
    }
    public string? ClaimOwner
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

    public bool IsActive => Status is not (RunDeliveryStatus.Merged or RunDeliveryStatus.Blocked or RunDeliveryStatus.Failed or RunDeliveryStatus.Cancelled);

    public static RunDelivery Create(Guid projectId, Guid runId, string defaultBranch, string baseSha, string runBranch, DateTimeOffset? at = null)
    {
        if (projectId == Guid.Empty || runId == Guid.Empty)
            throw new ArgumentException("Run delivery identities are required.");
        var now = at ?? DateTimeOffset.UtcNow;
        return new RunDelivery
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            PipelineRunId = runId,
            SourceDefaultBranch = Required(defaultBranch, 200),
            SourceBaseCommitSha = Required(baseSha, 64),
            RunBranchName = Required(runBranch, 250),
            Status = RunDeliveryStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void StartPreparing(DateTimeOffset? at = null) => Move(RunDeliveryStatus.Pending, RunDeliveryStatus.Preparing, at);
    public void RecordRunBranch(string headSha, DateTimeOffset? at = null)
    {
        Ensure(RunDeliveryStatus.Preparing);
        RunBranchHeadSha = Required(headSha, 64);
        Set(RunDeliveryStatus.RunBranchCreated, at);
    }
    public void StartTaskIntegration(DateTimeOffset? at = null)
    {
        if (Status is not (RunDeliveryStatus.RunBranchCreated or RunDeliveryStatus.IntegratingTasks))
            throw Invalid("Run branch must exist before task integration.");
        Set(RunDeliveryStatus.IntegratingTasks, at);
    }
    public void RecordIntegratedHead(string headSha, DateTimeOffset? at = null)
    {
        Ensure(RunDeliveryStatus.IntegratingTasks);
        RunBranchHeadSha = Required(headSha, 64);
        UpdatedAtUtc = at ?? DateTimeOffset.UtcNow;
    }
    public void StartAggregateValidation(DateTimeOffset? at = null) => Move(RunDeliveryStatus.IntegratingTasks, RunDeliveryStatus.AggregateValidation, at);
    public void RecordAggregateValidation(string summaryJson, DateTimeOffset? at = null)
    {
        Ensure(RunDeliveryStatus.AggregateValidation);
        AggregateValidationSummaryJson = Required(summaryJson, 16000);
        Set(RunDeliveryStatus.FinalReview, at);
    }
    public void RequestChanges(DateTimeOffset? at = null) => Move(RunDeliveryStatus.FinalReview, RunDeliveryStatus.ChangesRequested, at);
    public void ResumeFinalReview(string newHeadSha, DateTimeOffset? at = null)
    {
        Ensure(RunDeliveryStatus.ChangesRequested);
        RunBranchHeadSha = Required(newHeadSha, 64);
        FinalReviewDecisionId = null;
        FinalReviewedHeadSha = null;
        Set(RunDeliveryStatus.FinalReview, at);
    }
    public void ApproveFinalReview(Guid decisionId, string reviewedHeadSha, DateTimeOffset? at = null)
    {
        Ensure(RunDeliveryStatus.FinalReview);
        if (decisionId == Guid.Empty || !string.Equals(Required(reviewedHeadSha, 64), RunBranchHeadSha, StringComparison.OrdinalIgnoreCase))
            throw Invalid("Final review must approve the exact run-branch head.");
        FinalReviewDecisionId = decisionId;
        FinalReviewedHeadSha = reviewedHeadSha;
        Set(RunDeliveryStatus.ReadyForFinalPullRequest, at);
    }
    public void RecordFinalPullRequest(string provider, string repository, long number, string url, string headSha, string baseBranch, DateTimeOffset? at = null)
    {
        Ensure(RunDeliveryStatus.ReadyForFinalPullRequest);
        if (number <= 0 || !string.Equals(Required(headSha, 64), RunBranchHeadSha, StringComparison.OrdinalIgnoreCase) || !string.Equals(baseBranch, SourceDefaultBranch, StringComparison.Ordinal))
            throw Invalid("Final pull request must match the approved run delivery.");
        FinalPullRequestProvider = Required(provider, 50);
        FinalPullRequestRepository = Required(repository, 300);
        FinalPullRequestNumber = number;
        FinalPullRequestUrl = Required(url, 1000);
        FinalPullRequestHeadSha = headSha;
        FinalPullRequestBaseBranch = baseBranch;
        Set(RunDeliveryStatus.FinalPullRequestOpen, at);
    }
    public void RecordMainReadiness(string mergeableState, string checksState, DateTimeOffset? at = null)
    {
        Ensure(RunDeliveryStatus.FinalPullRequestOpen);
        if (FinalReviewDecisionId is null || !string.Equals(FinalReviewedHeadSha, RunBranchHeadSha, StringComparison.OrdinalIgnoreCase))
            throw Invalid("A current final review is required.");
        FinalPullRequestMergeableState = Required(mergeableState, 50);
        RequiredChecksState = Required(checksState, 50);
        Set(RunDeliveryStatus.ReadyForMain, at);
    }
    public void RequestMerge(DateTimeOffset? at = null) => Move(RunDeliveryStatus.ReadyForMain, RunDeliveryStatus.MergeRequested, at);
    public void MarkMerged(DateTimeOffset? at = null)
    {
        Ensure(RunDeliveryStatus.MergeRequested);
        if (FinalPullRequestNumber is null)
            throw Invalid("Verified final pull-request evidence is required.");
        Set(RunDeliveryStatus.Merged, at, true);
    }
    public void Claim(Guid claimId, string owner, DateTimeOffset expiresAt, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        if (!IsActive || claimId == Guid.Empty || expiresAt <= now || ClaimExpiresAtUtc > now)
            throw Invalid("A valid available run-delivery claim is required.");
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
    public void Block(string code, string message, DateTimeOffset? at = null)
    {
        EnsureActive();
        FailureCode = Required(code, 100);
        FailureMessage = Required(message, 1000);
        Set(RunDeliveryStatus.Blocked, at, true);
    }
    public void Fail(string code, string message, DateTimeOffset? at = null)
    {
        EnsureActive();
        FailureCode = Required(code, 100);
        FailureMessage = Required(message, 1000);
        Set(RunDeliveryStatus.Failed, at, true);
    }
    public void Cancel(DateTimeOffset? at = null)
    {
        EnsureActive();
        Set(RunDeliveryStatus.Cancelled, at, true);
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw Invalid("Only an active run delivery can change.");
    }
    private void Move(RunDeliveryStatus expected, RunDeliveryStatus next, DateTimeOffset? at)
    {
        Ensure(expected);
        Set(next, at);
    }
    private void Ensure(RunDeliveryStatus expected)
    {
        if (Status != expected)
            throw Invalid($"Run delivery cannot move from {Status}; expected {expected}.");
    }
    private void Set(RunDeliveryStatus next, DateTimeOffset? at, bool complete = false)
    {
        Status = next;
        UpdatedAtUtc = at ?? DateTimeOffset.UtcNow;
        if (complete)
            CompletedAtUtc = UpdatedAtUtc;
    }
    private static string Required(string? value, int max)
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
