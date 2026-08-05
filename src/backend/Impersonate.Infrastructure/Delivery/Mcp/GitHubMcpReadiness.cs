namespace Impersonate.Infrastructure.Delivery.Mcp;

public sealed record GitHubMcpReadiness(bool Enabled, string Transport, string ServerId, string[] AllowedRepositories, string[] Tools, string TokenEnvironmentVariable, bool TokenAvailable, string Environment, string DataProtectionKeyLocation, bool DatabaseConfigured);
