namespace Impersonate.Application.Delivery;

public sealed record FinalPullRequestObservation(string HeadSha, bool Open, bool Merged, string MergeableState, string ChecksState, string? MergeCommitSha);
