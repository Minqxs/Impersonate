namespace Impersonate.Application.Quality;

public sealed record SaveProjectQualityConfigurationRequest(
    bool Enabled, string BaseUrl, string ProjectKey, string? DisplayName, string? Token);
