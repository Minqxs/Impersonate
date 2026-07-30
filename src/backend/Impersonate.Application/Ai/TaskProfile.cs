using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record TaskProfile(AgentRole Role, TaskComplexity Complexity, RiskLevel Risk, bool RequiresCoding, bool RequiresReasoning, bool RequiresStructuredOutput, bool RequiresTools, int EstimatedContextSize, Sensitivity CostSensitivity, Sensitivity LatencySensitivity, IReadOnlyList<string> Reasons, EngineeringTaskType TaskType = EngineeringTaskType.Unknown, IReadOnlyList<string>? Languages = null, IReadOnlyList<string>? Frameworks = null, IReadOnlyList<string>? AffectedAreas = null, string ChangeType = "Unknown", string ConflictRisk = "Unknown", bool DatabaseInvolvement = false, bool SecuritySensitive = false, bool ConcurrencySensitive = false, bool ArchitectureSensitive = false, int AttemptNumber = 0, int PriorFailures = 0, int RevisionCount = 0, string? ReviewerFeedback = null, int ExpectedDiffSize = 0);
