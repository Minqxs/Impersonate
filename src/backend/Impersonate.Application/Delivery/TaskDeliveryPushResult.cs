namespace Impersonate.Application.Delivery;

public sealed record TaskDeliveryPushResult(string RemoteName, string Repository, string BranchName, string CommitSha, bool Recovered);
