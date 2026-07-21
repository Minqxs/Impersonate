using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Ai;

internal sealed class DataProtectionCredentialStore(ImpersonateDbContext db, IDataProtectionProvider protection) : IProviderCredentialStore
{
    private readonly IDataProtector protector = protection.CreateProtector("Impersonate.ProviderCredentials.v1");
    public async Task StoreAsync(Guid id, ProviderCredential credential, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credential.ApiKey)) throw new ArgumentException("API key is required.");
        var payload = protector.Protect(JsonSerializer.Serialize(credential));
        var existing = await db.ProviderCredentialSecrets.SingleOrDefaultAsync(x => x.ConnectionId == id, ct);
        if (existing is null) db.ProviderCredentialSecrets.Add(new(id, payload)); else existing.Replace(payload);
        await db.SaveChangesAsync(ct);
    }
    public async Task<ProviderCredential?> RetrieveAsync(Guid id, CancellationToken ct) { var row = await db.ProviderCredentialSecrets.AsNoTracking().SingleOrDefaultAsync(x => x.ConnectionId == id, ct); return row is null ? null : JsonSerializer.Deserialize<ProviderCredential>(protector.Unprotect(row.ProtectedPayload)); }
    public async Task DeleteAsync(Guid id, CancellationToken ct) { var row = await db.ProviderCredentialSecrets.SingleOrDefaultAsync(x => x.ConnectionId == id, ct); if (row is not null) { db.ProviderCredentialSecrets.Remove(row); await db.SaveChangesAsync(ct); } }
}
