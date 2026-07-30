using System.Text.Json;
using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

internal sealed class DeterministicModelRouter(IAiRoutingRepository repository, ITaskProfiler profiler, IModelCapabilityCatalog catalog, IModelIdentityClassifier classifier) : IModelRouter
{
    public async Task<ModelSelectionResult> SelectAsync(ModelSelectionRequest request, CancellationToken ct)
    {
        var profile = profiler.Profile(request);
        var policy = await repository.GetPolicyAsync(request.ProjectId, ct) ?? ProjectAiRoutingPolicy.Create(request.ProjectId);
        var connections = (await repository.GetConnectionsAsync(ct)).Where(x => x.Status == ProviderConnectionStatus.Connected).ToDictionary(x => x.Id);
        var allowed = JsonSerializer.Deserialize<ProviderType[]>(policy.AllowedProvidersJson) ?? [];
        var blocked = JsonSerializer.Deserialize<ProviderType[]>(policy.BlockedProvidersJson) ?? [];
        var candidates = (await repository.GetModelsAsync(null, ct)).Where(x => x.IsAvailable && x.LifecycleStatus != ModelLifecycleStatus.Deprecated && connections.ContainsKey(x.ProviderConnectionId) && (policy.AllowPreviewModels || x.LifecycleStatus != ModelLifecycleStatus.Preview) && !(request.ExcludedModels?.Contains(x.Id) ?? false) && (allowed.Length == 0 || allowed.Contains(x.ProviderType)) && !blocked.Contains(x.ProviderType)).Select(x => (Model: x, Discovery: Parse(x.CapabilitiesJson), Catalog: catalog.Resolve(x.ProviderType, x.ProviderModelId))).Where(x => SupportsRequest(request.Role, profile, x.Discovery, x.Catalog, x.Model, request)).Select(x => Score(x.Model, x.Discovery, x.Catalog, profile, policy, request, classifier)).OrderByDescending(x => x.Score).ThenBy(x => x.ProviderType).ThenBy(x => x.ProviderModelId, StringComparer.Ordinal).ToList();
        var overrideId = request.ManualModelOverrideId ?? policy.FixedModelOverrideId;
        if (candidates.Count == 0)
            return overrideId is not null ? new(false, profile, null, [], "invalid_override", "The selected model is unavailable or does not meet this role's requirements.") : new(false, profile, null, [], "no_eligible_model", connections.Count == 0 ? "No connected AI providers are available." : $"No discovered model satisfies the {request.Role} requirements and project policy.");
        var best = candidates[0];
        var ranked = candidates.Skip(1).Select(x => x with { RankedLowerReason = $"Ranked {best.Score - x.Score} points lower than {best.ProviderModelId} under the same role profile and project policy." }).ToList();
        if (overrideId is not null)
        {
            var chosen = candidates.FirstOrDefault(x => x.DiscoveredModelId == overrideId);
            if (chosen is null)
                return new(false, profile, null, candidates, "invalid_override", "The selected model is unavailable or does not meet this role's requirements.");
            chosen = chosen with
            {
                Source = ModelSelectionSource.ManualOverride,
                Explanation = "Selected by an explicit task override after role-specific compatibility validation."
            };
            return new(true, profile, chosen, candidates.Where(x => x.DiscoveredModelId != overrideId).ToList(), null, null);
        }

        return new(true, profile, best, ranked, null, null);
    }

    private static bool SupportsRequest(AgentRole role, TaskProfile profile, ModelCapability discovered, ModelCapabilityProfile model, DiscoveredModel record, ModelSelectionRequest request)
    {
        if (!model.SupportedRoles.Contains(role) || !discovered.HasFlag(ModelCapability.TextGeneration) || model.Endpoint == ProviderEndpoint.Unknown || record.ContextWindowSize.GetValueOrDefault(int.MaxValue) < profile.EstimatedContextSize)
            return false;
        var floor = profile.Complexity switch
        {
            TaskComplexity.High => 4,
            TaskComplexity.Moderate => 3,
            _ => 2
        };
        if (role == AgentRole.Planner)
            floor = profile.Complexity == TaskComplexity.High ? 4 : 1;
        if (role == AgentRole.Reviewer)
            return model.ReviewStrength >= floor && model.StructuredOutputStrength >= floor;
        if (role == AgentRole.Coder && !(model.CodingStrength >= floor && model.RepositoryToolReliability >= floor && model.AgenticCodingTier >= floor))
            return false;
        var failure = request.FailureHistory?.LastOrDefault();
        if (failure?.Code == "coder_protocol_failed" && role == AgentRole.Coder && !(model.CodingStrength > failure.CodingStrength.GetValueOrDefault() || model.RepositoryToolReliability > failure.RepositoryToolReliability.GetValueOrDefault()))
            return false;
        if (failure?.Code == "invalid_output" && model.StructuredOutputStrength <= failure.StructuredOutputReliability.GetValueOrDefault())
            return false;
        return true;
    }

    private static SelectedModel Score(DiscoveredModel model, ModelCapability discovered, ModelCapabilityProfile metadata, TaskProfile profile, ProjectAiRoutingPolicy policy, ModelSelectionRequest request, IModelIdentityClassifier classifier)
    {
        var floor = profile.Complexity switch
        {
            TaskComplexity.High => 4,
            TaskComplexity.Moderate => 3,
            _ => 2
        };
        var parts = new List<ScoreComponent>
        {
            new("Hard compatibility", 20, $"Meets the {profile.Role} capability floor {floor}/4.")
        };
        var role = profile.Role switch
        {
            AgentRole.Coder => metadata.CodingStrength,
            AgentRole.Reviewer => metadata.ReviewStrength,
            _ => metadata.PlanningStrength
        };
        parts.Add(new("Role fit", role * 8, $"Catalogue v2 role-fit tier is {role}/4 for {profile.Role}."));
        parts.Add(new("Repository protocol", profile.RequiresTools ? metadata.RepositoryToolReliability * 4 : 0, $"Reviewed repository-protocol reliability is {metadata.RepositoryToolReliability}/4."));
        var taskFit = (profile.Languages ?? []).Count(x => metadata.LanguageAffinities.Contains(x)) * 4 + (profile.Frameworks ?? []).Count(x => metadata.FrameworkAffinities.Contains(x)) * 4;
        parts.Add(new("Task and stack fit", taskFit, taskFit > 0 ? "Catalogue affinities overlap the detected repository stack." : "No reviewed language/framework affinity bonus was applied."));
        parts.Add(new("Complexity and risk", metadata.ReasoningStrength * (profile.Complexity == TaskComplexity.High ? 4 : 2), $"Reasoning tier was weighted for {profile.Complexity} complexity and {profile.Risk} risk."));
        var qualityMode = policy.CostPreference == RoutingPreference.Quality || policy.LatencyPreference == RoutingPreference.Quality;
        var economyMode = policy.CostPreference == RoutingPreference.Economy;
        parts.Add(new("Policy", qualityMode ? (role + metadata.ReasoningStrength + metadata.RepositoryToolReliability) * 5 : economyMode ? (5 - metadata.CostTier) * 12 : (role + metadata.ReasoningStrength) * 3 + (5 - metadata.CostTier) * 4, $"{(qualityMode ? "Quality" : economyMode ? "Economy" : "Balanced")} policy was applied only after the capability floor."));
        if (policy.PreferredProvider == model.ProviderType)
            parts.Add(new("Preferred provider", 10, "The model belongs to the project's preferred provider."));
        ScoreComponent? diversity = null;
        if (profile.Role == AgentRole.Reviewer && policy.PreferReviewerDiversity && request.CoderModelId is not null)
        {
            var coderFamily = request.CoderProvider is { } cp ? classifier.Classify(cp, request.FailureHistory?.LastOrDefault()?.Model ?? string.Empty).CanonicalModel : string.Empty;
            var current = classifier.Classify(model.ProviderType, model.ProviderModelId);
            var different = request.CoderProvider != model.ProviderType || (!string.IsNullOrEmpty(coderFamily) && current.CanonicalModel != coderFamily);
            diversity = new("Reviewer diversity", different ? policy.ReviewerDiversityWeight : 0, different ? "Reviewer has a materially different provider or canonical capability identity." : "Alias or dated snapshot identity is not meaningful Reviewer diversity.");
            parts.Add(diversity);
        }

        parts.Add(new("Historical outcomes", 0, "Historical performance was not used because insufficient samples exist (minimum 10)."));
        var total = parts.Sum(x => x.Score);
        var explanation = string.Join(" ", parts.Where(x => x.Score > 0).OrderByDescending(x => x.Score).Take(5).Select(x => x.Reason));
        if (request.FailureHistory?.LastOrDefault() is { } failure)
            explanation += $" Escalated after {failure.Model} failed with {failure.Code}.";
        var identity = classifier.Classify(model.ProviderType, model.ProviderModelId);
        return new(model.ProviderConnectionId, model.Id, model.ProviderType, model.ProviderModelId, request.FailureHistory?.Count > 0 ? ModelSelectionSource.Escalation : ModelSelectionSource.AutomaticRouting, total, explanation, parts, metadata.MetadataVersion, null, identity.CanonicalModel, identity.Variant.ToString(), identity.Endpoint.ToString(), $"{profile.Role} {floor}/4", model.ContextWindowSize, model.MaximumOutputSize);
    }

    private static ModelCapability Parse(string json)
    {
        try
        {
            return (ModelCapability)(JsonSerializer.Deserialize<int>(json));
        }
        catch
        {
            return ModelCapability.None;
        }
    }
}
