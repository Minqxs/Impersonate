using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

internal sealed class VersionedModelCapabilityCatalog(IModelIdentityClassifier classifier) : IModelCapabilityCatalog
{
    private const string Version = "catalog-2026-07-v3";
    private static readonly HashSet<AgentRole> All = [AgentRole.Planner, AgentRole.Coder, AgentRole.Reviewer];
    private static readonly HashSet<AgentRole> PlannerOnly = [AgentRole.Planner];
    private static readonly HashSet<AgentRole> PlannerReviewer = [AgentRole.Planner, AgentRole.Reviewer];
    private static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase) { "C#", "TypeScript", "JavaScript", "Python" };
    private static readonly HashSet<string> Frameworks = new(StringComparer.OrdinalIgnoreCase) { ".NET", "React" };

    public ModelCapabilityProfile Resolve(ProviderType provider, string modelId)
    {
        var identity = classifier.Classify(provider, modelId);
        if (!identity.IsKnown)
            return Create(provider, identity, PlannerOnly, 1, 1, 1, 1, 1, 1, 4, 2, 1, 0, 0, true, "Availability was discovered, but role capability has not been reviewed.");

        if (provider != ProviderType.OpenAI)
            return Create(provider, identity, All, 3, 3, 3, 3, 3, 4, 2, 2, 3, 0, 0, false, "Provider-native autonomous repository tools are not implemented, so Coder routing is ineligible.");

        var id = identity.CanonicalModel;
        if (id == "gpt-4.1")
            return Create(provider, identity, All, 4, 3, 3, 4, 4, 4, 3, 2, 4, 4, 4, false, "Older non-reasoning generation; reliable function calling but below current agentic coding models.");
        if (id == "gpt-4.1-mini")
            return Create(provider, identity, All, 3, 2, 3, 4, 4, 4, 1, 1, 3, 3, 3, false, "Efficient older non-reasoning model; use only above the task capability floor.");
        if (id == "gpt-4.1-nano")
            return Create(provider, identity, PlannerReviewer, 1, 1, 2, 3, 3, 3, 1, 1, 2, 1, 1, false, "Optimized for simple high-volume tasks, not autonomous repository mutation.");
        if (identity.Variant == ModelVariant.Coding)
            return Create(provider, identity, All, 4, 4, 3, 3, 4, 4, 3, 3, 2, 4, 4, false, "Coding-specialized Responses model; review/planning strength is intentionally not treated as flagship-general strength.");
        if (identity.Variant == ModelVariant.Nano)
            return Create(provider, identity, PlannerReviewer, 2, 2, 2, 4, 4, 4, 1, 1, 2, 2, 1, false, "Cost-focused model for simple tasks; below the autonomous Coder capability floor.");
        if (identity.Variant is ModelVariant.Mini or ModelVariant.Balanced)
            return Create(provider, identity, All, identity.Variant == ModelVariant.Balanced ? 4 : 3, 3, 3, 4, 4, 4, identity.Variant == ModelVariant.Balanced ? 2 : 1, identity.Variant == ModelVariant.Balanced ? 2 : 1, 3, 4, 3, false, "Efficient current-generation model; quality remains below the flagship/coding-specialized tier.");
        if (identity.CanonicalFamily.StartsWith("gpt-5", StringComparison.Ordinal) || identity.CanonicalFamily is "o3" or "o4")
            return Create(provider, identity, All, 4, 4, 4, 4, 4, 4, identity.Variant == ModelVariant.Pro ? 4 : 3, 3, 4, 4, 4, false, identity.Variant == ModelVariant.Pro ? "Higher-cost precision model." : "General reasoning model; coding-specialized siblings may be preferred for repository mutation.");
        return Create(provider, identity, PlannerOnly, 1, 1, 1, 1, 1, 1, 4, 2, 1, 0, 0, true, "Known identity without reviewed autonomous role claims.");
    }

    private static ModelCapabilityProfile Create(ProviderType provider, ModelIdentity identity, IReadOnlySet<AgentRole> roles, int coding, int reasoning, int review, int structured, int tools, int context, int cost, int latency, int planning, int repositoryTools, int agentic, bool conservative, string limitations) =>
        new(identity.CanonicalModel, roles, coding, reasoning, review, structured, tools, context, cost, latency, Languages, Frameworks, Version, conservative, planning, repositoryTools, agentic, identity.Endpoint, identity.CanonicalFamily, identity.Variant, "official-provider-docs", "2026-07-30", !conservative, provider, identity.CanonicalFamily, identity.Variant.ToString(), limitations);
}
