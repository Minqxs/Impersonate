using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ProviderConnectionContext(Guid ConnectionId, ProviderType ProviderType, ProviderCredential Credential);
