namespace Impersonate.Application.Quality;

public sealed record ProjectQualityConfigurationDto(
    bool Configured, bool Enabled, string? BaseUrl, string? ProjectKey,
    string? DisplayName, bool CredentialConfigured, DateTimeOffset? LastSuccessfulRefreshAtUtc,
    string? LastFailureCode, string? LastSafeFailureMessage);
