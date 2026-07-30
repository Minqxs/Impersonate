using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record RoutingModelIdentity(Guid? DiscoveredModelId, ProviderType Provider, string ProviderModelId, string CanonicalFamily, string Generation, string Specialisation);
