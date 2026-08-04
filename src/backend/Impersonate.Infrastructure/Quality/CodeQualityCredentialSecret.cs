namespace Impersonate.Infrastructure.Quality;

public sealed class CodeQualityCredentialSecret
{
    private CodeQualityCredentialSecret()
    {
    }
    public Guid ConfigurationId
    {
        get; private set;
    }
    public string ProtectedPayload { get; private set; } = null!;
    public DateTimeOffset UpdatedAtUtc
    {
        get; private set;
    }
    public CodeQualityCredentialSecret(Guid id, string payload)
    {
        ConfigurationId = id;
        Replace(payload);
    }
    public void Replace(string payload)
    {
        ProtectedPayload = payload;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
