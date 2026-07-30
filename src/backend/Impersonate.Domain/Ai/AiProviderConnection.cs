namespace Impersonate.Domain.Ai;

public sealed class AiProviderConnection
{
    private AiProviderConnection()
    {
    }

    public Guid Id
    {
        get; private set;
    }
    public ProviderType ProviderType
    {
        get; private set;
    }
    public string DisplayName { get; private set; } = null!;
    public ProviderConnectionStatus Status
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
    public DateTimeOffset? LastValidatedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset? LastModelSyncAtUtc
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
    public IReadOnlyCollection<DiscoveredModel> Models => models;

    private readonly List<DiscoveredModel> models = [];
    public static AiProviderConnection Create(ProviderType type, string displayName, DateTimeOffset? now = null)
    {
        if (type is ProviderType.AzureOpenAI or ProviderType.AmazonBedrock or ProviderType.Ollama)
            throw new ArgumentOutOfRangeException(nameof(type), "Provider is not supported yet.");
        var at = now ?? DateTimeOffset.UtcNow;
        return new()
        {
            Id = Guid.NewGuid(),
            ProviderType = type,
            DisplayName = Required(displayName, 100),
            Status = ProviderConnectionStatus.PendingValidation,
            CreatedAtUtc = at,
            UpdatedAtUtc = at
        };
    }

    public void Connected(DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        Status = ProviderConnectionStatus.Connected;
        LastValidatedAtUtc = UpdatedAtUtc = at;
        LastFailureCode = LastSafeFailureMessage = null;
    }

    public void ValidationFailed(bool credentials, string code, string message, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        Status = credentials ? ProviderConnectionStatus.InvalidCredentials : ProviderConnectionStatus.Unavailable;
        LastValidatedAtUtc = UpdatedAtUtc = at;
        LastFailureCode = Required(code, 100);
        LastSafeFailureMessage = Required(message, 500);
    }

    public void Synchronised(DateTimeOffset? now = null)
    {
        LastModelSyncAtUtc = UpdatedAtUtc = now ?? DateTimeOffset.UtcNow;
    }

    public void Disable()
    {
        Status = ProviderConnectionStatus.Disabled;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Enable()
    {
        Status = ProviderConnectionStatus.PendingValidation;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void CredentialsReplaced(DateTimeOffset? now = null)
    {
        Status = ProviderConnectionStatus.PendingValidation;
        LastFailureCode = null;
        LastSafeFailureMessage = null;
        UpdatedAtUtc = now ?? DateTimeOffset.UtcNow;
    }

    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException("Value is required and limited.") : value.Trim();
}
