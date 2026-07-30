using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ProviderCredential(string ApiKey, string? Organisation = null, string? Project = null);
