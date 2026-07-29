using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

[Flags] public enum ModelCapability { None=0, TextGeneration=1, StructuredOutput=2, Reasoning=4, Coding=8, ToolUse=16, LargeContext=32, FastResponse=64, LowCost=128, Vision=256 }
public enum TaskComplexity { Simple, Moderate, High }
public enum RiskLevel { Low, Moderate, High }
public enum Sensitivity { Low, Balanced, High }
public enum ModelVariant { Unknown, Flagship, Pro, Balanced, Mini, Nano, Coding }
public enum ProviderEndpoint { Unknown, ChatCompletions, Responses, Messages, GenerateContent }
public sealed record ModelIdentity(ProviderType Provider,string CanonicalFamily,string CanonicalModel,string? Snapshot,ModelVariant Variant,ProviderEndpoint Endpoint,string RateLimitFamily,bool IsKnown,bool IsMalformed=false);
public interface IModelIdentityClassifier { ModelIdentity Classify(ProviderType provider,string modelId); }
public sealed record ProviderCredential(string ApiKey, string? Organisation = null, string? Project = null);
public enum ProviderCredentialReadStatus { Found, Missing, Unreadable }
public sealed record ProviderCredentialReadResult(ProviderCredentialReadStatus Status,ProviderCredential? Credential,string? SafeFailureCode,string? SafeFailureMessage);
public sealed class ProviderCredentialStorageException:Exception { public ProviderCredentialStorageException():base("The provider credential could not be stored safely."){} }
public sealed class ProviderCredentialUnavailableException(string code,string safeMessage):Exception(safeMessage) { public string Code{get;}=code; }
public enum RateLimitScope { Requests, Tokens, ConcurrentRequests, Unknown }
public sealed record ProviderCapacityMetadata(System.Net.HttpStatusCode StatusCode,string? ProviderRequestId=null,TimeSpan? RetryAfter=null,TimeSpan? RequestReset=null,TimeSpan? TokenReset=null,long? RequestLimit=null,long? RemainingRequests=null,long? TokenLimit=null,long? RemainingTokens=null,RateLimitScope Scope=RateLimitScope.Unknown,bool TemporaryCapacity=false,bool QuotaExhausted=false);
public sealed class ProviderRequestException(string code,string safeMessage,System.Net.HttpStatusCode statusCode,bool isTransient,ProviderCapacityMetadata? capacity=null):Exception(safeMessage) { public string Code{get;}=code;public System.Net.HttpStatusCode StatusCode{get;}=statusCode;public bool IsTransient{get;}=isTransient;public ProviderCapacityMetadata? Capacity{get;}=capacity; }
public sealed record ProviderConnectionContext(Guid ConnectionId, ProviderType ProviderType, ProviderCredential Credential);
public sealed record ProviderValidationResult(bool Succeeded, bool InvalidCredentials, string? FailureCode, string SafeMessage);
public sealed record ProviderModel(string Id, string Name, string? Description, ModelLifecycleStatus Lifecycle, ModelCapability Capabilities, CapabilityMetadataSource CapabilitySource, int? ContextWindow, int? MaximumOutput);
public sealed record RoutedModel(Guid? DiscoveredModelId, string ProviderModelId);
public sealed record LanguageModelRequest(string Model,string SystemInstructions,string UserContent,string JsonSchema,int MaximumOutputTokens,string? ReasoningEffort=null,string? TextVerbosity=null);
public sealed record LanguageModelResponse(string Content,string? ProviderRequestId,int? InputTokenCount,int? OutputTokenCount,int SameModelRequestAttemptCount=1,int RateLimitRetryCount=0,long CumulativeRateLimitWaitMilliseconds=0,RateLimitScope? LastRateLimitScope=null,bool ProviderResetUsed=false,string? ResponseStatus=null,string? IncompleteReason=null,IReadOnlyList<string>? OutputItemTypes=null,int OutputTextLength=0,int? ReasoningTokenCount=null,string? SafeFailureCode=null);

public interface IProviderCredentialStore
{
    Task StoreAsync(Guid connectionId, ProviderCredential credential, CancellationToken cancellationToken);
    Task<ProviderCredentialReadResult> RetrieveAsync(Guid connectionId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid connectionId, CancellationToken cancellationToken);
}
public interface IAiProviderAdapter
{
    ProviderType ProviderType { get; }
    Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext connection, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext connection, CancellationToken cancellationToken);
    Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext connection, RoutedModel model, LanguageModelRequest request, CancellationToken cancellationToken);
}
public enum EngineeringTaskType { DomainModel,DatabaseMigration,BackendApplication,ApiEndpoint,FrontendState,FrontendUi,Testing,BuildConfiguration,Security,Refactoring,Documentation,Unknown }
public sealed record TaskProfile(AgentRole Role, TaskComplexity Complexity, RiskLevel Risk, bool RequiresCoding, bool RequiresReasoning, bool RequiresStructuredOutput, bool RequiresTools, int EstimatedContextSize, Sensitivity CostSensitivity, Sensitivity LatencySensitivity, IReadOnlyList<string> Reasons,EngineeringTaskType TaskType=EngineeringTaskType.Unknown,IReadOnlyList<string>? Languages=null,IReadOnlyList<string>? Frameworks=null,IReadOnlyList<string>? AffectedAreas=null,string ChangeType="Unknown",string ConflictRisk="Unknown",bool DatabaseInvolvement=false,bool SecuritySensitive=false,bool ConcurrencySensitive=false,bool ArchitectureSensitive=false,int AttemptNumber=0,int PriorFailures=0,int RevisionCount=0,string? ReviewerFeedback=null,int ExpectedDiffSize=0);
public interface ITaskProfiler { TaskProfile Profile(AgentRole role,string description); TaskProfile Profile(ModelSelectionRequest request); }
public sealed record ScoreComponent(string Name,int Score,string Reason);
public sealed record PriorModelFailure(string Code,ProviderType Provider,string Model,int? CodingStrength=null,int? RepositoryToolReliability=null,int? StructuredOutputReliability=null,int? ContextTier=null,Guid? DecisionId=null);
public sealed record ModelSelectionRequest(Guid ProjectId, Guid? PipelineRunId, AgentRole Role, string Description, Guid? ManualModelOverrideId = null, IReadOnlySet<Guid>? ExcludedModels = null,string? TaskTitle=null,IReadOnlyList<string>? AcceptanceCriteria=null,string? FeatureRequest=null,IReadOnlyList<string>? RepositoryLanguages=null,IReadOnlyList<string>? RepositoryFrameworks=null,string ChangeType="Unknown",IReadOnlyList<string>? AffectedAreas=null,string Risk="Unknown",string ConflictRisk="Unknown",int AttemptNumber=0,int PriorFailures=0,int RevisionCount=0,string? ReviewerFeedback=null,Guid? CoderModelId=null,ProviderType? CoderProvider=null,IReadOnlyList<string>? RepositoryEvidence=null,int ExpectedFileCount=0,int ExpectedDiffSize=0,IReadOnlyList<PriorModelFailure>? FailureHistory=null);
public sealed record SelectedModel(Guid? ConnectionId, Guid? DiscoveredModelId, ProviderType ProviderType, string ProviderModelId, ModelSelectionSource Source, int Score, string Explanation,IReadOnlyList<ScoreComponent>? ScoreBreakdown=null,string MetadataVersion="catalog-2026-07-v2",string? RankedLowerReason=null,string? CanonicalFamily=null,string? Variant=null,string? Endpoint=null,string? RequiredCapabilityFloor=null,int? ContextWindowSize=null,int? MaximumOutputSize=null);
public static class ModelRateLimitFamily { public static string Get(ProviderType provider,string model)=>provider==ProviderType.OpenAI?System.Text.RegularExpressions.Regex.Replace(model,@"-\d{4}-\d{2}-\d{2}$",string.Empty,System.Text.RegularExpressions.RegexOptions.CultureInvariant).ToLowerInvariant():model.ToLowerInvariant();public static bool Matches(ProviderType provider,string left,string right)=>Get(provider,left)==Get(provider,right); }
public sealed record ModelSelectionResult(bool Succeeded, TaskProfile Profile, SelectedModel? Selection, IReadOnlyList<SelectedModel> EligibleAlternatives, string? FailureCode, string? FailureMessage);
public interface IModelRouter { Task<ModelSelectionResult> SelectAsync(ModelSelectionRequest request, CancellationToken cancellationToken); }
public sealed record ModelCapabilityProfile(string ProviderModelPattern,IReadOnlySet<AgentRole> SupportedRoles,int CodingStrength,int ReasoningStrength,int ReviewStrength,int StructuredOutputStrength,int ToolUseStrength,int ContextTier,int CostTier,int LatencyTier,IReadOnlySet<string> LanguageAffinities,IReadOnlySet<string> FrameworkAffinities,string MetadataVersion,bool IsConservativeDefault=false,int PlanningStrength=1,int RepositoryToolReliability=0,int AgenticCodingTier=0,ProviderEndpoint Endpoint=ProviderEndpoint.Unknown,string CanonicalFamily="unknown",ModelVariant Variant=ModelVariant.Unknown,string MetadataSource="conservative",string ReviewedDate="2026-07-27",bool QualityClaimsReviewed=false);
public interface IModelCapabilityCatalog { ModelCapabilityProfile Resolve(ProviderType provider,string modelId); }
public sealed record ProjectAiReadiness(int ConnectedProviderCount,int ValidProviderCount,int DiscoveredEligiblePlannerModels,string RoutingStatus,IReadOnlyList<string> Blockers);
public interface IProjectAiService
{
    Task<ProjectAiReadiness?> GetReadinessAsync(Guid projectId,CancellationToken cancellationToken);
    Task<ModelSelectionResult?> PreviewAsync(Guid projectId,AgentRole role,string description,Guid? manualModelOverrideId,CancellationToken cancellationToken);
}
public sealed record ModelUsageSummary(string Provider,string Model,int AttemptCount,int SuccessfulPlanCount,int InvalidOutputCount,int ProviderFailureCount,int TimedOutCount,long InputTokenCount,long OutputTokenCount,double AverageDurationMilliseconds,double ValidPlanRate);
public interface IModelUsageService { Task<IReadOnlyList<ModelUsageSummary>> GetPlanningUsageAsync(int days,CancellationToken cancellationToken); }
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
public sealed record ReplaceProviderCredentialRequest(string ApiKey,string? Organisation=null,string? Project=null);
public sealed record ProviderConnectionDto(Guid Id, ProviderType ProviderType, string DisplayName, ProviderConnectionStatus Status, DateTimeOffset? LastValidatedAtUtc, DateTimeOffset? LastModelSyncAtUtc, int AvailableModelCount, string? LastFailureCode, string? LastSafeFailureMessage);
public sealed record DiscoveredModelDto(Guid Id, Guid ProviderConnectionId, ProviderType ProviderType, string ProviderModelId, string DisplayName, string? Description, ModelLifecycleStatus LifecycleStatus, bool IsAvailable, int? ContextWindowSize, int? MaximumOutputSize);
public interface IAiProviderConnectionService
{
    Task<IReadOnlyList<ProviderConnectionDto>> ListAsync(CancellationToken cancellationToken);
    Task<ProviderConnectionDto> CreateAsync(ProviderType providerType, CreateProviderConnectionRequest request, CancellationToken cancellationToken);
    Task<ProviderConnectionDto?> ReplaceCredentialsAsync(Guid connectionId,ReplaceProviderCredentialRequest request,CancellationToken cancellationToken);
    Task<ProviderConnectionDto?> ValidateAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<ProviderConnectionDto?> SynchroniseAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DiscoveredModelDto>?> ModelsAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<bool> DisableAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(Guid connectionId, CancellationToken cancellationToken);
}
