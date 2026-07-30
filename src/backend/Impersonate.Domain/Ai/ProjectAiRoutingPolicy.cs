namespace Impersonate.Domain.Ai;

public sealed class ProjectAiRoutingPolicy
{
    private ProjectAiRoutingPolicy()
    {
    }

    public Guid ProjectId
    {
        get; private set;
    }
    public RoutingPreference CostPreference { get; private set; } = RoutingPreference.Balanced;
    public RoutingPreference LatencyPreference { get; private set; } = RoutingPreference.Balanced;
    public bool AllowPreviewModels
    {
        get; private set;
    }
    public bool AllowAutomaticEscalation { get; private set; } = true;
    public int MaximumEscalationCount { get; private set; } = 1;
    public ProviderType? PreferredProvider
    {
        get; private set;
    }
    public Guid? FixedModelOverrideId
    {
        get; private set;
    }
    public bool PreferReviewerDiversity { get; private set; } = true;
    public int ReviewerDiversityWeight { get; private set; } = 12;
    public string AllowedProvidersJson { get; private set; } = "[]";
    public string BlockedProvidersJson { get; private set; } = "[]";

    public static ProjectAiRoutingPolicy Create(Guid projectId) => projectId == Guid.Empty ? throw new ArgumentException("Project is required.") : new()
    {
        ProjectId = projectId
    };
    public void Update(RoutingPreference cost, RoutingPreference latency, bool previews, bool escalation, int max, ProviderType? preferred, Guid? model, string allowed, string blocked, bool preferReviewerDiversity = true, int reviewerDiversityWeight = 12)
    {
        if (max is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(max));
        if (reviewerDiversityWeight is < 0 or > 50)
            throw new ArgumentOutOfRangeException(nameof(reviewerDiversityWeight));
        CostPreference = cost;
        LatencyPreference = latency;
        AllowPreviewModels = previews;
        AllowAutomaticEscalation = escalation;
        MaximumEscalationCount = max;
        PreferredProvider = preferred;
        FixedModelOverrideId = model;
        AllowedProvidersJson = allowed;
        BlockedProvidersJson = blocked;
        PreferReviewerDiversity = preferReviewerDiversity;
        ReviewerDiversityWeight = reviewerDiversityWeight;
    }
}
