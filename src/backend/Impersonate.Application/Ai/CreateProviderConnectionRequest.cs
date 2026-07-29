using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record CreateProviderConnectionRequest(string DisplayName, string ApiKey, string? Organisation = null, string? Project = null);
