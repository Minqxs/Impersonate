namespace Impersonate.Application.Delivery;

public sealed record PullRequestReference(string Provider, string Repository, long Number, string SafeUrl);
