using System.Text.Json;
using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

internal sealed class DeterministicTaskProfiler : ITaskProfiler
{
    private static readonly string[] HighSignals = ["architecture", "migration", "security", "concurrency", "ambiguous", "redesign", "integration"];
    public TaskProfile Profile(AgentRole role, string description)
    {
        var text = description ?? string.Empty;
        var high = text.Length > 1200 || HighSignals.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase));
        var simple = text.Length < 300 && !high;
        var complexity = high ? TaskComplexity.High : simple ? TaskComplexity.Simple : TaskComplexity.Moderate;
        var reasons = new List<string> { high ? "Architecture, risk, ambiguity, or context signals require stronger reasoning." : simple ? "The request is short and well-defined." : "The request has moderate scope." };
        return new(role, complexity, high ? RiskLevel.High : RiskLevel.Moderate, role == AgentRole.Coder, true, role == AgentRole.Planner, role != AgentRole.Planner, Math.Max(2000, text.Length * 3), high ? Sensitivity.Low : Sensitivity.Balanced, simple ? Sensitivity.High : Sensitivity.Balanced, reasons);
    }
}

internal sealed class DeterministicModelRouter(IAiRoutingRepository repository, ITaskProfiler profiler) : IModelRouter
{
    public async Task<ModelSelectionResult> SelectAsync(ModelSelectionRequest request, CancellationToken ct)
    {
        var profile = profiler.Profile(request.Role, request.Description);
        var policy = await repository.GetPolicyAsync(request.ProjectId, ct) ?? ProjectAiRoutingPolicy.Create(request.ProjectId);
        var connections = (await repository.GetConnectionsAsync(ct)).Where(x => x.Status == ProviderConnectionStatus.Connected).ToDictionary(x => x.Id);
        var models = (await repository.GetModelsAsync(null, ct)).Where(x => x.IsAvailable && x.LifecycleStatus != ModelLifecycleStatus.Deprecated && SupportsPlannerRequest(x) && connections.ContainsKey(x.ProviderConnectionId) && (policy.AllowPreviewModels || x.LifecycleStatus != ModelLifecycleStatus.Preview) && !(request.ExcludedModels?.Contains(x.Id) ?? false)).ToList();
        var allowed = JsonSerializer.Deserialize<ProviderType[]>(policy.AllowedProvidersJson) ?? [];
        var blocked = JsonSerializer.Deserialize<ProviderType[]>(policy.BlockedProvidersJson) ?? [];
        models = models.Where(x => (allowed.Length == 0 || allowed.Contains(x.ProviderType)) && !blocked.Contains(x.ProviderType)).ToList();
        var scored = models.Select(x => (Model:x, Capabilities:Parse(x.CapabilitiesJson))).Where(x => x.Capabilities.HasFlag(ModelCapability.TextGeneration) && (!profile.RequiresStructuredOutput || x.Capabilities.HasFlag(ModelCapability.StructuredOutput)) && (!profile.RequiresTools || x.Capabilities.HasFlag(ModelCapability.ToolUse)) && x.Model.ContextWindowSize.GetValueOrDefault(int.MaxValue) >= profile.EstimatedContextSize).Select(x => new SelectedModel(x.Model.ProviderConnectionId, x.Model.Id, x.Model.ProviderType, x.Model.ProviderModelId, ModelSelectionSource.AutomaticRouting, Score(x.Model, x.Capabilities, profile, policy), Explain(profile, policy))).OrderByDescending(x => x.Score).ThenBy(x => x.ProviderType).ThenBy(x => x.ProviderModelId, StringComparer.Ordinal).ToList();
        var overrideId = request.ManualModelOverrideId ?? policy.FixedModelOverrideId;
        if (overrideId is not null)
        {
            var chosen = scored.FirstOrDefault(x => x.DiscoveredModelId == overrideId);
            if (chosen is null) return new(false, profile, null, scored, "invalid_override", "The selected model is unavailable or does not meet this task's requirements.");
            chosen = chosen with { Source = ModelSelectionSource.ManualOverride, Explanation = "Selected by an explicit advanced override after capability validation." };
            return new(true, profile, chosen, scored.Where(x => x.DiscoveredModelId != overrideId).ToList(), null, null);
        }
        if (scored.Count == 0) return new(false, profile, null, [], "no_eligible_model", connections.Count == 0 ? "No connected AI providers are available." : "No discovered model satisfies the Planner requirements and project policy.");
        return new(true, profile, scored[0], scored.Skip(1).ToList(), null, null);
    }
    private static ModelCapability Parse(string json) { try { return (ModelCapability)(JsonSerializer.Deserialize<int>(json)); } catch { return ModelCapability.None; } }
    private static bool SupportsPlannerRequest(DiscoveredModel model)=>model.ProviderType!=ProviderType.OpenAI||!(model.ProviderModelId.StartsWith("gpt-3.5",StringComparison.OrdinalIgnoreCase)||model.ProviderModelId.Equals("gpt-4",StringComparison.OrdinalIgnoreCase)||model.ProviderModelId.StartsWith("gpt-4-",StringComparison.OrdinalIgnoreCase));
    private static int Score(DiscoveredModel model, ModelCapability capabilities, TaskProfile profile, ProjectAiRoutingPolicy policy) { var score = 100; if (capabilities.HasFlag(ModelCapability.Reasoning)) score += profile.Complexity == TaskComplexity.High ? 40 : 15; if (capabilities.HasFlag(ModelCapability.StructuredOutput)) score += 20; if (model.ContextWindowSize >= profile.EstimatedContextSize * 2) score += 10; if (policy.PreferredProvider == model.ProviderType) score += 15; if (capabilities.HasFlag(ModelCapability.LowCost)) score += policy.CostPreference == RoutingPreference.Economy ? 25 : 5; if (capabilities.HasFlag(ModelCapability.FastResponse)) score += policy.LatencyPreference == RoutingPreference.Economy ? 25 : 5; return score; }
    private static string Explain(TaskProfile profile, ProjectAiRoutingPolicy policy) => $"Selected because the request requires {profile.Complexity.ToString().ToLowerInvariant()} reasoning and structured output, and the model matched the project's {policy.CostPreference} cost policy.";
}
