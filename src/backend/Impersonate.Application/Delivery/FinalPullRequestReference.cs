namespace Impersonate.Application.Delivery;

public sealed record FinalPullRequestReference(string Provider, string Repository, long Number, string Url, string HeadSha, string BaseBranch);
