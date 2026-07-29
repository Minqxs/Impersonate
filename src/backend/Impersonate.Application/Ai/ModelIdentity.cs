using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ModelIdentity(ProviderType Provider, string CanonicalFamily, string CanonicalModel, string? Snapshot, ModelVariant Variant, ProviderEndpoint Endpoint, string RateLimitFamily, bool IsKnown, bool IsMalformed = false);
