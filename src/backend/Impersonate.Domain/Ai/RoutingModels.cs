namespace Impersonate.Domain.Ai;

public sealed class ProjectAiRoutingPolicy
{
    private ProjectAiRoutingPolicy() { }
    public Guid ProjectId { get; private set; }
    public RoutingPreference CostPreference { get; private set; } = RoutingPreference.Balanced;
    public RoutingPreference LatencyPreference { get; private set; } = RoutingPreference.Balanced;
    public bool AllowPreviewModels { get; private set; }
    public bool AllowAutomaticEscalation { get; private set; } = true;
    public int MaximumEscalationCount { get; private set; } = 1;
    public ProviderType? PreferredProvider { get; private set; }
    public Guid? FixedModelOverrideId { get; private set; }
    public string AllowedProvidersJson { get; private set; } = "[]";
    public string BlockedProvidersJson { get; private set; } = "[]";
    public static ProjectAiRoutingPolicy Create(Guid projectId) => projectId == Guid.Empty ? throw new ArgumentException("Project is required.") : new() { ProjectId = projectId };
    public void Update(RoutingPreference cost, RoutingPreference latency, bool previews, bool escalation, int max, ProviderType? preferred, Guid? model, string allowed, string blocked) { if (max is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(max)); CostPreference = cost; LatencyPreference = latency; AllowPreviewModels = previews; AllowAutomaticEscalation = escalation; MaximumEscalationCount = max; PreferredProvider = preferred; FixedModelOverrideId = model; AllowedProvidersJson = allowed; BlockedProvidersJson = blocked; }
}

public sealed class ModelSelectionDecision
{
    private ModelSelectionDecision() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? PipelineRunId { get; private set; }
    public AgentRole Role { get; private set; }
    public Guid? ProviderConnectionId { get; private set; }
    public Guid? DiscoveredModelId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public ModelSelectionSource SelectionSource { get; private set; }
    public int Score { get; private set; }
    public string TaskProfileJson { get; private set; } = null!;
    public string Explanation { get; private set; } = null!;
    public string CandidateSummaryJson { get; private set; } = "[]";
    public Guid? EscalatedFromDecisionId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static ModelSelectionDecision Create(Guid project, Guid? run, AgentRole role, Guid? connection, Guid? discovered, string provider, string model, ModelSelectionSource source, int score, string profile, string explanation, string candidates, Guid? prior = null) => new() { Id = Guid.NewGuid(), ProjectId = project, PipelineRunId = run, Role = role, ProviderConnectionId = connection, DiscoveredModelId = discovered, Provider = provider, Model = model, SelectionSource = source, Score = score, TaskProfileJson = profile, Explanation = explanation, CandidateSummaryJson = candidates, EscalatedFromDecisionId = prior, CreatedAtUtc = DateTimeOffset.UtcNow };
}
