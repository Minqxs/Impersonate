namespace Impersonate.Infrastructure.Delivery.Mcp;

public sealed record DevelopmentPreflight(bool DatabaseConnected, bool MigrationsCurrent, bool DataProtectionWritable, bool GitAvailable, bool GitHubMcpEnabled, bool TargetRepositoryValid, bool TargetRepositoryAllowed, bool TokenAvailable, bool OfficialServerConfigured, bool RequiredToolsConfigured);
