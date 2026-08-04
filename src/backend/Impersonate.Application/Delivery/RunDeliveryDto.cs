using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

public sealed record RunDeliveryDto(Guid Id, RunDeliveryStatus Status, string SourceDefaultBranch, string SourceBaseCommitSha,
    string RunBranchName, string? RunBranchHeadSha, string AggregateValidationSummaryJson, Guid? FinalReviewDecisionId,
    string? FinalReviewedHeadSha, string? FinalPullRequestRepository, long? FinalPullRequestNumber, string? FinalPullRequestUrl,
    string? FinalPullRequestHeadSha, string? FinalPullRequestBaseBranch, string? FinalPullRequestMergeableState,
    string? RequiredChecksState, string? FailureCode, string? FailureMessage);
