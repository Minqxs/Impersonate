namespace Impersonate.Domain.Ai;

public sealed class AiProviderConnection
{
    private AiProviderConnection() { }
    public Guid Id { get; private set; }
    public ProviderType ProviderType { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public ProviderConnectionStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? LastValidatedAtUtc { get; private set; }
    public DateTimeOffset? LastModelSyncAtUtc { get; private set; }
    public string? LastFailureCode { get; private set; }
    public string? LastSafeFailureMessage { get; private set; }
    public IReadOnlyCollection<DiscoveredModel> Models => models;
    private readonly List<DiscoveredModel> models = [];

    public static AiProviderConnection Create(ProviderType type, string displayName, DateTimeOffset? now = null)
    {
        if (type is ProviderType.AzureOpenAI or ProviderType.AmazonBedrock or ProviderType.Ollama) throw new ArgumentOutOfRangeException(nameof(type), "Provider is not supported yet.");
        var at = now ?? DateTimeOffset.UtcNow;
        return new() { Id = Guid.NewGuid(), ProviderType = type, DisplayName = Required(displayName, 100), Status = ProviderConnectionStatus.PendingValidation, CreatedAtUtc = at, UpdatedAtUtc = at };
    }
    public void Connected(DateTimeOffset? now = null) { var at = now ?? DateTimeOffset.UtcNow; Status = ProviderConnectionStatus.Connected; LastValidatedAtUtc = UpdatedAtUtc = at; LastFailureCode = LastSafeFailureMessage = null; }
    public void ValidationFailed(bool credentials, string code, string message, DateTimeOffset? now = null) { var at = now ?? DateTimeOffset.UtcNow; Status = credentials ? ProviderConnectionStatus.InvalidCredentials : ProviderConnectionStatus.Unavailable; LastValidatedAtUtc = UpdatedAtUtc = at; LastFailureCode = Required(code, 100); LastSafeFailureMessage = Required(message, 500); }
    public void Synchronised(DateTimeOffset? now = null) { LastModelSyncAtUtc = UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }
    public void Disable() { Status = ProviderConnectionStatus.Disabled; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public void Enable() { Status = ProviderConnectionStatus.PendingValidation; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException("Value is required and limited.") : value.Trim();
}

public sealed class DiscoveredModel
{
    private DiscoveredModel() { }
    public Guid Id { get; private set; }
    public Guid ProviderConnectionId { get; private set; }
    public ProviderType ProviderType { get; private set; }
    public string ProviderModelId { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? Description { get; private set; }
    public ModelLifecycleStatus LifecycleStatus { get; private set; }
    public DateTimeOffset DiscoveredAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }
    public bool IsAvailable { get; private set; }
    public CapabilityMetadataSource CapabilitySource { get; private set; }
    public string CapabilitiesJson { get; private set; } = "{}";
    public int? ContextWindowSize { get; private set; }
    public int? MaximumOutputSize { get; private set; }
    public static DiscoveredModel Create(Guid connection, ProviderType provider, string id, string name, string? description, ModelLifecycleStatus lifecycle, CapabilityMetadataSource source, string capabilities, int? context, int? output, DateTimeOffset? now = null) { if (connection == Guid.Empty) throw new ArgumentException("Connection is required."); if (string.IsNullOrWhiteSpace(id) || id.Length > 300) throw new ArgumentException("Model ID is invalid."); var at = now ?? DateTimeOffset.UtcNow; return new() { Id = Guid.NewGuid(), ProviderConnectionId = connection, ProviderType = provider, ProviderModelId = id, DisplayName = string.IsNullOrWhiteSpace(name) ? id : name, Description = description, LifecycleStatus = lifecycle, CapabilitySource = source, CapabilitiesJson = capabilities, ContextWindowSize = context, MaximumOutputSize = output, DiscoveredAtUtc = at, LastSeenAtUtc = at, IsAvailable = true }; }
    public void Refresh(string name, string? description, ModelLifecycleStatus lifecycle, CapabilityMetadataSource source, string capabilities, int? context, int? output) { DisplayName = name; Description = description; LifecycleStatus = lifecycle; CapabilitySource = source; CapabilitiesJson = capabilities; ContextWindowSize = context; MaximumOutputSize = output; LastSeenAtUtc = DateTimeOffset.UtcNow; IsAvailable = true; }
    public void MarkUnavailable() => IsAvailable = false;
}
