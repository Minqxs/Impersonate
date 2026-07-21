using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

[Flags] public enum ModelCapability { None=0, TextGeneration=1, StructuredOutput=2, Reasoning=4, Coding=8, ToolUse=16, LargeContext=32, FastResponse=64, LowCost=128, Vision=256 }
public enum TaskComplexity { Simple, Moderate, High }
public enum RiskLevel { Low, Moderate, High }
public enum Sensitivity { Low, Balanced, High }
public sealed record ProviderCredential(string ApiKey, string? Organisation = null, string? Project = null);
public sealed record ProviderConnectionContext(Guid ConnectionId, ProviderType ProviderType, ProviderCredential Credential);
public sealed record ProviderValidationResult(bool Succeeded, bool InvalidCredentials, string? FailureCode, string SafeMessage);
public sealed record ProviderModel(string Id, string Name, string? Description, ModelLifecycleStatus Lifecycle, ModelCapability Capabilities, CapabilityMetadataSource CapabilitySource, int? ContextWindow, int? MaximumOutput);
public sealed record RoutedModel(Guid? DiscoveredModelId, string ProviderModelId);
public sealed record LanguageModelRequest(string Model,string SystemInstructions,string UserContent,string JsonSchema);
public sealed record LanguageModelResponse(string Content,string? ProviderRequestId,int? InputTokenCount,int? OutputTokenCount);

public interface IProviderCredentialStore
{
    Task StoreAsync(Guid connectionId, ProviderCredential credential, CancellationToken cancellationToken);
    Task<ProviderCredential?> RetrieveAsync(Guid connectionId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid connectionId, CancellationToken cancellationToken);
}
public interface IAiProviderAdapter
{
    ProviderType ProviderType { get; }
    Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext connection, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext connection, CancellationToken cancellationToken);
    Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext connection, RoutedModel model, LanguageModelRequest request, CancellationToken cancellationToken);
}
public sealed record TaskProfile(AgentRole Role, TaskComplexity Complexity, RiskLevel Risk, bool RequiresCoding, bool RequiresReasoning, bool RequiresStructuredOutput, bool RequiresTools, int EstimatedContextSize, Sensitivity CostSensitivity, Sensitivity LatencySensitivity, IReadOnlyList<string> Reasons);
public interface ITaskProfiler { TaskProfile Profile(AgentRole role, string description); }
public sealed record ModelSelectionRequest(Guid ProjectId, Guid? PipelineRunId, AgentRole Role, string Description, Guid? ManualModelOverrideId = null, IReadOnlySet<Guid>? ExcludedModels = null);
public sealed record SelectedModel(Guid? ConnectionId, Guid? DiscoveredModelId, ProviderType ProviderType, string ProviderModelId, ModelSelectionSource Source, int Score, string Explanation);
public sealed record ModelSelectionResult(bool Succeeded, TaskProfile Profile, SelectedModel? Selection, IReadOnlyList<SelectedModel> EligibleAlternatives, string? FailureCode, string? FailureMessage);
public interface IModelRouter { Task<ModelSelectionResult> SelectAsync(ModelSelectionRequest request, CancellationToken cancellationToken); }
public sealed record ProjectAiReadiness(int ConnectedProviderCount,int ValidProviderCount,int DiscoveredEligiblePlannerModels,string RoutingStatus,IReadOnlyList<string> Blockers);
public interface IProjectAiService
{
    Task<ProjectAiReadiness?> GetReadinessAsync(Guid projectId,CancellationToken cancellationToken);
    Task<ModelSelectionResult?> PreviewAsync(Guid projectId,AgentRole role,string description,Guid? manualModelOverrideId,CancellationToken cancellationToken);
}
public interface IAiRoutingRepository
{
    Task<IReadOnlyList<AiProviderConnection>> GetConnectionsAsync(CancellationToken cancellationToken);
    Task<AiProviderConnection?> GetConnectionAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DiscoveredModel>> GetModelsAsync(Guid? connectionId, CancellationToken cancellationToken);
    Task<ProjectAiRoutingPolicy?> GetPolicyAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ModelSelectionDecision?> GetDecisionAsync(Guid projectId, Guid runId, CancellationToken cancellationToken);
    Task AddConnectionAsync(AiProviderConnection connection, CancellationToken cancellationToken);
    Task AddModelAsync(DiscoveredModel model, CancellationToken cancellationToken);
    Task RemoveConnectionAsync(AiProviderConnection connection, CancellationToken cancellationToken);
    Task<ProjectAiRoutingPolicy> GetOrCreatePolicyAsync(Guid projectId, CancellationToken cancellationToken);
    Task AddDecisionAsync(ModelSelectionDecision decision, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
public sealed record CreateProviderConnectionRequest(string DisplayName, string ApiKey, string? Organisation = null, string? Project = null);
public sealed record ProviderConnectionDto(Guid Id, ProviderType ProviderType, string DisplayName, ProviderConnectionStatus Status, DateTimeOffset? LastValidatedAtUtc, DateTimeOffset? LastModelSyncAtUtc, int AvailableModelCount, string? LastFailureCode, string? LastSafeFailureMessage);
public sealed record DiscoveredModelDto(Guid Id, Guid ProviderConnectionId, ProviderType ProviderType, string ProviderModelId, string DisplayName, string? Description, ModelLifecycleStatus LifecycleStatus, bool IsAvailable, int? ContextWindowSize, int? MaximumOutputSize);
public interface IAiProviderConnectionService
{
    Task<IReadOnlyList<ProviderConnectionDto>> ListAsync(CancellationToken cancellationToken);
    Task<ProviderConnectionDto> CreateAsync(ProviderType providerType, CreateProviderConnectionRequest request, CancellationToken cancellationToken);
    Task<ProviderConnectionDto?> ValidateAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<ProviderConnectionDto?> SynchroniseAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DiscoveredModelDto>?> ModelsAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<bool> DisableAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(Guid connectionId, CancellationToken cancellationToken);
}
