using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ProjectAiReadiness(int ConnectedProviderCount, int ValidProviderCount, int DiscoveredEligiblePlannerModels, string RoutingStatus, IReadOnlyList<string> Blockers);
