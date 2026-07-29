using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record SelectedModel(Guid? ConnectionId, Guid? DiscoveredModelId, ProviderType ProviderType, string ProviderModelId, ModelSelectionSource Source, int Score, string Explanation, IReadOnlyList<ScoreComponent>? ScoreBreakdown = null, string MetadataVersion = "catalog-2026-07-v2", string? RankedLowerReason = null, string? CanonicalFamily = null, string? Variant = null, string? Endpoint = null, string? RequiredCapabilityFloor = null, int? ContextWindowSize = null, int? MaximumOutputSize = null);
