using Impersonate.Application.Projects;
namespace Impersonate.Application.Quality;

public sealed class ProjectQualityService(IProjectRepository projects, IProjectQualityRepository configurations, ICodeQualityCredentialStore credentials, ICodeQualityProvider provider, IProjectQualityCache cache, TimeProvider clock) : IProjectQualityService
{
    public async Task<ProjectQualityConfigurationDto?> GetConfigurationAsync(Guid id, CancellationToken ct)
    {
        if (await projects.GetAsync(id, ct) is null)
            return null;
        var x = await configurations.GetAsync(id, ct);
        return Map(x, x is not null && await credentials.RetrieveAsync(x.Id, ct) is not null);
    }
    public async Task<ProjectQualityConfigurationDto?> SaveAsync(Guid id, SaveProjectQualityConfigurationRequest r, CancellationToken ct)
    {
        if (await projects.GetAsync(id, ct) is null)
            return null;
        ValidateUri(r.BaseUrl);
        var x = await configurations.GetAsync(id, ct);
        if (x is null)
        {
            x = Domain.Quality.ProjectCodeQualityConfiguration.Create(id, r.Enabled, r.BaseUrl, r.ProjectKey, r.DisplayName, clock.GetUtcNow());
            await configurations.AddAsync(x, ct);
        }
        else
            x.Update(r.Enabled, r.BaseUrl, r.ProjectKey, r.DisplayName, clock.GetUtcNow());
        if (!string.IsNullOrWhiteSpace(r.Token))
            await credentials.StoreAsync(x.Id, r.Token, ct);
        await configurations.SaveChangesAsync(ct);
        cache.Remove(id);
        return Map(x, await credentials.RetrieveAsync(x.Id, ct) is not null);
    }
    public Task<ProjectQualitySummary?> ValidateAsync(Guid id, CancellationToken ct) => GetSummaryAsync(id, true, ct);
    public async Task<ProjectQualitySummary?> GetSummaryAsync(Guid id, bool force, CancellationToken ct)
    {
        if (!force && cache.TryGet(id, out var cached))
            return cached;
        if (await projects.GetAsync(id, ct) is null)
            return null;
        var x = await configurations.GetAsync(id, ct);
        if (x is null || !x.Enabled)
            return Empty(ProjectQualityState.NotConfigured, "quality_not_configured", "Code quality is not configured.");
        var token = await credentials.RetrieveAsync(x.Id, ct);
        if (string.IsNullOrWhiteSpace(token))
            return Empty(ProjectQualityState.AuthenticationRequired, "quality_credentials_missing", "A SonarQube token is required.");
        var result = await provider.GetSummaryAsync(new(new Uri(x.BaseUrl), x.ProjectKey, token), ct);
        ProjectQualitySummary summary;
        if (result.Succeeded)
        {
            x.RecordSuccess(clock.GetUtcNow());
            summary = result.Summary! with
            {
                LastSuccessfulRefreshAtUtc = x.LastSuccessfulRefreshAtUtc
            };
        }
        else
        {
            x.RecordFailure(result.FailureCode!, result.SafeMessage!, clock.GetUtcNow());
            summary = Empty(result.FailureState, result.FailureCode!, result.SafeMessage!, x.LastSuccessfulRefreshAtUtc);
        }
        await configurations.SaveChangesAsync(ct);
        cache.Set(id, summary, TimeSpan.FromMinutes(5));
        return summary;
    }
    public async Task<bool> RemoveAsync(Guid id, CancellationToken ct)
    {
        var x = await configurations.GetAsync(id, ct);
        if (x is null)
            return false;
        await credentials.DeleteAsync(x.Id, ct);
        configurations.Remove(x);
        await configurations.SaveChangesAsync(ct);
        cache.Remove(id);
        return true;
    }
    private static ProjectQualityConfigurationDto Map(Domain.Quality.ProjectCodeQualityConfiguration? x, bool credential) => x is null ? new(false, false, null, null, null, false, null, null, null) : new(true, x.Enabled, x.BaseUrl, x.ProjectKey, x.DisplayName, credential, x.LastSuccessfulRefreshAtUtc, x.LastFailureCode, x.LastSafeFailureMessage);
    private static ProjectQualitySummary Empty(ProjectQualityState state, string code, string message, DateTimeOffset? refreshed = null) => new(state, null, new(null), new(null), new(null), new(null), new(null), new(null), new(null), new(null), new(null), new(null), new(null), refreshed, code, message, null);
    private static void ValidateUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("SonarQube base URL must be an absolute HTTP or HTTPS URL without credentials, query, or fragment.", nameof(value));
    }
}
