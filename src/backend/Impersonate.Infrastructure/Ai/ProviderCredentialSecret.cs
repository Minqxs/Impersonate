namespace Impersonate.Infrastructure.Ai;

public sealed class ProviderCredentialSecret
{
    private ProviderCredentialSecret()
    {
    }
    public Guid ConnectionId
    {
        get; private set;
    }
    public string ProtectedPayload { get; private set; } = null!;
    public DateTimeOffset UpdatedAtUtc
    {
        get; private set;
    }
    public ProviderCredentialSecret(Guid connectionId, string protectedPayload)
    {
        ConnectionId = connectionId;
        ProtectedPayload = protectedPayload;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
    public void Replace(string protectedPayload)
    {
        ProtectedPayload = protectedPayload;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
