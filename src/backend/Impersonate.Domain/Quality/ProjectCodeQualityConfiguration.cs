namespace Impersonate.Domain.Quality;

public sealed class ProjectCodeQualityConfiguration
{
    private ProjectCodeQualityConfiguration()
    {
    }
    private ProjectCodeQualityConfiguration(Guid projectId, bool enabled, string baseUrl, string projectKey, string? displayName, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        ProjectId = projectId;
        Update(enabled, baseUrl, projectKey, displayName, now);
    }
    public Guid Id
    {
        get; private set;
    }
    public Guid ProjectId
    {
        get; private set;
    }
    public bool Enabled
    {
        get; private set;
    }
    public string BaseUrl { get; private set; } = null!;
    public string ProjectKey { get; private set; } = null!;
    public string? DisplayName
    {
        get; private set;
    }
    public DateTimeOffset? LastSuccessfulRefreshAtUtc
    {
        get; private set;
    }
    public string? LastFailureCode
    {
        get; private set;
    }
    public string? LastSafeFailureMessage
    {
        get; private set;
    }
    public DateTimeOffset UpdatedAtUtc
    {
        get; private set;
    }
    public static ProjectCodeQualityConfiguration Create(Guid projectId, bool enabled, string baseUrl, string projectKey, string? displayName, DateTimeOffset? now = null)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("Project is required.", nameof(projectId));
        return new(projectId, enabled, baseUrl, projectKey, displayName, now ?? DateTimeOffset.UtcNow);
    }
    public void Update(bool enabled, string baseUrl, string projectKey, string? displayName, DateTimeOffset? now = null)
    {
        Enabled = enabled;
        BaseUrl = Required(baseUrl, nameof(baseUrl), 500).TrimEnd('/');
        ProjectKey = Required(projectKey, nameof(projectKey), 400);
        DisplayName = Optional(displayName, 200);
        UpdatedAtUtc = now ?? DateTimeOffset.UtcNow;
    }
    public void RecordSuccess(DateTimeOffset now)
    {
        LastSuccessfulRefreshAtUtc = now;
        LastFailureCode = null;
        LastSafeFailureMessage = null;
        UpdatedAtUtc = now;
    }
    public void RecordFailure(string code, string message, DateTimeOffset now)
    {
        LastFailureCode = Required(code, nameof(code), 100);
        LastSafeFailureMessage = Required(message, nameof(message), 500);
        UpdatedAtUtc = now;
    }
    private static string Required(string value, string name, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrWhiteSpace(v))
            throw new ArgumentException("Value is required.", name);
        if (v.Length > max)
            throw new ArgumentOutOfRangeException(name, $"Value must not exceed {max} characters.");
        return v;
    }
    private static string? Optional(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrWhiteSpace(v))
            return null;
        if (v.Length > max)
            throw new ArgumentOutOfRangeException(nameof(value), $"Value must not exceed {max} characters.");
        return v;
    }
}
