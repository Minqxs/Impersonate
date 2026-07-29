using System.Text.Json;
using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

internal sealed class VersionedModelCapabilityCatalog(IModelIdentityClassifier classifier) : IModelCapabilityCatalog
{
    private const string Version = "catalog-2026-07-v2";
    private static readonly HashSet<AgentRole> All = [AgentRole.Planner, AgentRole.Coder, AgentRole.Reviewer];
    public ModelCapabilityProfile Resolve(ProviderType provider, string modelId)
    {
        var identity = classifier.Classify(provider, modelId);
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "C#",
            "TypeScript",
            "JavaScript",
            "Python"
        };
        var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".NET",
            "React"
        };
        var tiers = identity.Variant switch
        {
            ModelVariant.Pro => (4, 4, 4, 4, 4, 4, 4, 4),
            ModelVariant.Flagship => (4, 4, 4, 4, 4, 4, 4, 4),
            ModelVariant.Coding => (4, 4, 4, 4, 4, 4, 4, 4),
            ModelVariant.Balanced => (3, 3, 3, 3, 3, 3, 3, 3),
            ModelVariant.Mini => (3, 3, 3, 3, 3, 3, 3, 2),
            ModelVariant.Nano => (1, 2, 1, 2, 1, 2, 1, 0),
            _ => (1, 1, 1, 1, 1, 1, 1, 0)
        };
        return new(modelId, identity.IsKnown ? All : new HashSet<AgentRole> { AgentRole.Planner }, tiers.Item1, tiers.Item2, tiers.Item3, tiers.Item4, tiers.Item5, tiers.Item6, identity.Variant is ModelVariant.Mini or ModelVariant.Nano ? 1 : 3, identity.Variant is ModelVariant.Mini or ModelVariant.Nano ? 1 : 3, identity.IsKnown ? languages : new HashSet<string>(), identity.IsKnown ? frameworks : new HashSet<string>(), Version, !identity.IsKnown, tiers.Item2, tiers.Item7, tiers.Item8, identity.Endpoint, identity.CanonicalModel, identity.Variant, "official-provider-docs", "2026-07-27", identity.IsKnown);
    }
}
