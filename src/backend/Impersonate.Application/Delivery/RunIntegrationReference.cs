namespace Impersonate.Application.Delivery;

public sealed record RunIntegrationReference(string Repository, string DefaultBranch, string DefaultBranchHeadSha, string RunBranch, string RunBranchHeadSha);
