using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record DiscoveredModelDto(Guid Id, Guid ProviderConnectionId, ProviderType ProviderType, string ProviderModelId, string DisplayName, string? Description, ModelLifecycleStatus LifecycleStatus, bool IsAvailable, int? ContextWindowSize, int? MaximumOutputSize);
