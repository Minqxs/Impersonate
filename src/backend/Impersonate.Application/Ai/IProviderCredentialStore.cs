using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public interface IProviderCredentialStore
{
    Task StoreAsync(Guid connectionId, ProviderCredential credential, CancellationToken cancellationToken);
    Task<ProviderCredentialReadResult> RetrieveAsync(Guid connectionId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid connectionId, CancellationToken cancellationToken);
}
