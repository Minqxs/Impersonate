using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ModelCapabilityProfile(string ProviderModelPattern, IReadOnlySet<AgentRole> SupportedRoles, int CodingStrength, int ReasoningStrength, int ReviewStrength, int StructuredOutputStrength, int ToolUseStrength, int ContextTier, int CostTier, int LatencyTier, IReadOnlySet<string> LanguageAffinities, IReadOnlySet<string> FrameworkAffinities, string MetadataVersion, bool IsConservativeDefault = false, int PlanningStrength = 1, int RepositoryToolReliability = 0, int AgenticCodingTier = 0, ProviderEndpoint Endpoint = ProviderEndpoint.Unknown, string CanonicalFamily = "unknown", ModelVariant Variant = ModelVariant.Unknown, string MetadataSource = "conservative", string ReviewedDate = "2026-07-27", bool QualityClaimsReviewed = false);
