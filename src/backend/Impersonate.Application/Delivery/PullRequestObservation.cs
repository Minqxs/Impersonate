namespace Impersonate.Application.Delivery;

public sealed record PullRequestObservation(
    string Provider,
    string Repository,
    long Number,
    string HeadBranch,
    string BaseBranch,
    string HeadSha,
    PullRequestExternalState State,
    string? MergeCommitSha);
