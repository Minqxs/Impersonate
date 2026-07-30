namespace Impersonate.Domain.Projects;

public sealed class Project
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const int RepositoryUrlMaxLength = 500;
    public const int DefaultBranchMaxLength = 200;

    private Project()
    {
    }

    private Project(Guid id, string name, string? description, string repositoryUrl, string defaultBranch, ProjectStatus status, DateTimeOffset now)
    {
        Id = id;
        UpdateDetails(name, description, repositoryUrl, defaultBranch, now);
        ChangeStatus(status, now);
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id
    {
        get; private set;
    }
    public string Name { get; private set; } = null!;
    public string? Description
    {
        get; private set;
    }
    public string RepositoryUrl { get; private set; } = null!;
    public string DefaultBranch { get; private set; } = null!;
    public ProjectStatus Status
    {
        get; private set;
    }
    public DateTimeOffset CreatedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset UpdatedAtUtc
    {
        get; private set;
    }

    public static Project Create(string name, string? description, string repositoryUrl, string defaultBranch, ProjectStatus status = ProjectStatus.Idle, DateTimeOffset? now = null) =>
        new(Guid.NewGuid(), name, description, repositoryUrl, defaultBranch, status, now ?? DateTimeOffset.UtcNow);

    public void UpdateDetails(string name, string? description, string repositoryUrl, string defaultBranch, DateTimeOffset? now = null)
    {
        Name = Required(name, nameof(name), NameMaxLength);
        Description = Optional(description, DescriptionMaxLength);
        RepositoryUrl = ValidateRepositoryUrl(repositoryUrl);
        DefaultBranch = Required(defaultBranch, nameof(defaultBranch), DefaultBranchMaxLength);
        UpdatedAtUtc = now ?? DateTimeOffset.UtcNow;
    }

    public void ChangeStatus(ProjectStatus status, DateTimeOffset? now = null)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status), "Project status must be defined.");
        Status = status;
        UpdatedAtUtc = now ?? DateTimeOffset.UtcNow;
    }

    private static string Required(string value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Value is required.", parameterName);
        if (normalized.Length > maximumLength)
            throw new ArgumentOutOfRangeException(parameterName, $"Value must not exceed {maximumLength} characters.");
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (normalized.Length > maximumLength)
            throw new ArgumentOutOfRangeException(nameof(value), $"Value must not exceed {maximumLength} characters.");
        return normalized;
    }

    private static string ValidateRepositoryUrl(string value)
    {
        var normalized = Required(value, nameof(value), RepositoryUrlMaxLength);
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Segments.Length != 3 ||
            string.IsNullOrWhiteSpace(uri.Segments[1].Trim('/')) ||
            string.IsNullOrWhiteSpace(uri.Segments[2].Trim('/')) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Repository URL must be a GitHub HTTPS repository URL.", nameof(value));
        return normalized;
    }
}
