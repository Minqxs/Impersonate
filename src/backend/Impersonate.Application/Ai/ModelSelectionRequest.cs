using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ModelSelectionRequest(Guid ProjectId, Guid? PipelineRunId, AgentRole Role, string Description, Guid? ManualModelOverrideId = null, IReadOnlySet<Guid>? ExcludedModels = null, string? TaskTitle = null, IReadOnlyList<string>? AcceptanceCriteria = null, string? FeatureRequest = null, IReadOnlyList<string>? RepositoryLanguages = null, IReadOnlyList<string>? RepositoryFrameworks = null, string ChangeType = "Unknown", IReadOnlyList<string>? AffectedAreas = null, string Risk = "Unknown", string ConflictRisk = "Unknown", int AttemptNumber = 0, int PriorFailures = 0, int RevisionCount = 0, string? ReviewerFeedback = null, RoutingModelIdentity? CoderIdentity = null, IReadOnlyList<string>? RepositoryEvidence = null, int ExpectedFileCount = 0, int ExpectedDiffSize = 0, IReadOnlyList<PriorModelFailure>? FailureHistory = null);
