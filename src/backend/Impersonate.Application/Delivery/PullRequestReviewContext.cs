namespace Impersonate.Application.Delivery;

public sealed record PullRequestReviewContext(string HeadSha, string BaseSha, string Diff, IReadOnlyList<string> ChangedFiles);
