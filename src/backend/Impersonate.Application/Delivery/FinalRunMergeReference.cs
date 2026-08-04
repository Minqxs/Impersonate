namespace Impersonate.Application.Delivery;

public sealed record FinalRunMergeReference(string Repository, long PullRequestNumber, string PullRequestHeadSha, string MergeCommitSha);
