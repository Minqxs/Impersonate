using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public interface IAiProviderConnectionService
{
    Task<IReadOnlyList<ProviderConnectionDto>> ListAsync(CancellationToken cancellationToken);
    Task<ProviderConnectionDto> CreateAsync(ProviderType providerType, CreateProviderConnectionRequest request, CancellationToken cancellationToken);
    Task<ProviderConnectionDto?> ReplaceCredentialsAsync(Guid connectionId, ReplaceProviderCredentialRequest request, CancellationToken cancellationToken);
    Task<ProviderConnectionDto?> ValidateAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<ProviderConnectionDto?> SynchroniseAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DiscoveredModelDto>?> ModelsAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<bool> DisableAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(Guid connectionId, CancellationToken cancellationToken);
}
