using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Impersonate.Infrastructure.Ai;

internal sealed class DataProtectionCredentialStore(ImpersonateDbContext db, IDataProtectionProvider protection) : IProviderCredentialStore
{
    private readonly IDataProtector protector = protection.CreateProtector("Impersonate.ProviderCredentials.v1");
    public async Task StoreAsync(Guid id, ProviderCredential credential, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credential.ApiKey)) throw new ArgumentException("API key is required.");
        string payload;try{payload=protector.Protect(JsonSerializer.Serialize(credential with{ApiKey=credential.ApiKey.Trim()}));}catch(CryptographicException){throw new ProviderCredentialStorageException();}
        var existing = await db.ProviderCredentialSecrets.SingleOrDefaultAsync(x => x.ConnectionId == id, ct);
        if (existing is null) db.ProviderCredentialSecrets.Add(new(id, payload)); else existing.Replace(payload);
    }
    public async Task<ProviderCredentialReadResult> RetrieveAsync(Guid id, CancellationToken ct) { var row=await db.ProviderCredentialSecrets.AsNoTracking().SingleOrDefaultAsync(x=>x.ConnectionId==id,ct);if(row is null)return new(ProviderCredentialReadStatus.Missing,null,"credentials_missing","No saved credential is available. Replace credentials to repair this connection.");try{var value=JsonSerializer.Deserialize<ProviderCredential>(protector.Unprotect(row.ProtectedPayload));return value is null?new(ProviderCredentialReadStatus.Unreadable,null,"credentials_unreadable","The saved provider credential cannot be decrypted. Replace credentials after checking the shared key-ring configuration."):new(ProviderCredentialReadStatus.Found,value,null,null);}catch(CryptographicException){return new(ProviderCredentialReadStatus.Unreadable,null,"credentials_unreadable","The saved provider credential cannot be decrypted. Replace credentials after checking the shared key-ring configuration.");}catch(JsonException){return new(ProviderCredentialReadStatus.Unreadable,null,"credentials_unreadable","The saved provider credential cannot be decrypted. Replace credentials after checking the shared key-ring configuration.");} }
    public async Task DeleteAsync(Guid id, CancellationToken ct) { var row=await db.ProviderCredentialSecrets.SingleOrDefaultAsync(x=>x.ConnectionId==id,ct);if(row is not null)db.ProviderCredentialSecrets.Remove(row); }
}
