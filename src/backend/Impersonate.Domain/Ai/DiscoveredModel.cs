namespace Impersonate.Domain.Ai;

public sealed class DiscoveredModel
{
    private DiscoveredModel()
    {
    }

    public Guid Id
    {
        get; private set;
    }
    public Guid ProviderConnectionId
    {
        get; private set;
    }
    public ProviderType ProviderType
    {
        get; private set;
    }
    public string ProviderModelId { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? Description
    {
        get; private set;
    }
    public ModelLifecycleStatus LifecycleStatus
    {
        get; private set;
    }
    public DateTimeOffset DiscoveredAtUtc
    {
        get; private set;
    }
    public DateTimeOffset LastSeenAtUtc
    {
        get; private set;
    }
    public bool IsAvailable
    {
        get; private set;
    }
    public CapabilityMetadataSource CapabilitySource
    {
        get; private set;
    }
    public string CapabilitiesJson { get; private set; } = "{}";
    public int? ContextWindowSize
    {
        get; private set;
    }
    public int? MaximumOutputSize
    {
        get; private set;
    }

    public static DiscoveredModel Create(Guid connection, ProviderType provider, string id, string name, string? description, ModelLifecycleStatus lifecycle, CapabilityMetadataSource source, string capabilities, int? context, int? output, DateTimeOffset? now = null)
    {
        if (connection == Guid.Empty)
            throw new ArgumentException("Connection is required.");
        if (string.IsNullOrWhiteSpace(id) || id.Length > 300)
            throw new ArgumentException("Model ID is invalid.");
        var at = now ?? DateTimeOffset.UtcNow;
        return new()
        {
            Id = Guid.NewGuid(),
            ProviderConnectionId = connection,
            ProviderType = provider,
            ProviderModelId = id,
            DisplayName = string.IsNullOrWhiteSpace(name) ? id : name,
            Description = description,
            LifecycleStatus = lifecycle,
            CapabilitySource = source,
            CapabilitiesJson = capabilities,
            ContextWindowSize = context,
            MaximumOutputSize = output,
            DiscoveredAtUtc = at,
            LastSeenAtUtc = at,
            IsAvailable = true
        };
    }

    public void Refresh(string name, string? description, ModelLifecycleStatus lifecycle, CapabilityMetadataSource source, string capabilities, int? context, int? output)
    {
        DisplayName = name;
        Description = description;
        LifecycleStatus = lifecycle;
        CapabilitySource = source;
        CapabilitiesJson = capabilities;
        ContextWindowSize = context;
        MaximumOutputSize = output;
        LastSeenAtUtc = DateTimeOffset.UtcNow;
        IsAvailable = true;
    }

    public void MarkUnavailable() => IsAvailable = false;
}
