using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record RoutedModel(Guid? DiscoveredModelId, string ProviderModelId);
