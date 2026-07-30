using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record PlannedTaskDto(Guid Id, int Sequence, string Title, string Description, IReadOnlyList<string> AcceptanceCriteria, PlannedTaskStatus Status, int RevisionCount, int MaximumRevisionAttempts, Guid? CoderModelOverrideId, Guid? ReviewerModelOverrideId, IReadOnlyList<TaskAttemptDto> Attempts, IReadOnlyList<ReviewDecisionDto> Reviews, string? SkipReason, string? FailureReason, IReadOnlyList<Guid> DependsOnTaskIds, IReadOnlyList<string> AffectedAreas, string ChangeType, string Risk, string ConflictRisk, string? ExecutionReason, IReadOnlyList<string> RepositoryEvidence, int OriginalPlannerSequence, bool OrderAdjusted, string? OrderAdjustmentReason, bool EstablishesSharedContract);
