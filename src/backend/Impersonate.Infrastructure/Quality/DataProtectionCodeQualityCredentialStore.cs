using System.Security.Cryptography;
using Impersonate.Application.Quality;
using Impersonate.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
namespace Impersonate.Infrastructure.Quality;

internal sealed class DataProtectionCodeQualityCredentialStore(ImpersonateDbContext db, IDataProtectionProvider protection) : ICodeQualityCredentialStore
{
    private readonly IDataProtector protector = protection.CreateProtector("Impersonate.CodeQualityCredentials.v1");
    public async Task StoreAsync(Guid id, string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required.");
        string payload;
        try
        {
            payload = protector.Protect(token.Trim());
        }
        catch (CryptographicException) { throw new InvalidOperationException("The code-quality credential could not be protected."); }
        var row = await db.CodeQualityCredentialSecrets.SingleOrDefaultAsync(x => x.ConfigurationId == id, ct);
        if (row is null)
            db.CodeQualityCredentialSecrets.Add(new(id, payload));
        else
            row.Replace(payload);
    }
    public async Task<string?> RetrieveAsync(Guid id, CancellationToken ct)
    {
        var row = await db.CodeQualityCredentialSecrets.AsNoTracking().SingleOrDefaultAsync(x => x.ConfigurationId == id, ct);
        if (row is null)
            return null;
        try
        {
            return protector.Unprotect(row.ProtectedPayload);
        }
        catch (CryptographicException) { return null; }
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await db.CodeQualityCredentialSecrets.SingleOrDefaultAsync(x => x.ConfigurationId == id, ct);
        if (row is not null)
            db.CodeQualityCredentialSecrets.Remove(row);
    }
}
