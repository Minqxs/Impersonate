using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlannerTask(int Sequence, string Title, string Description, IReadOnlyList<string> AcceptanceCriteria, IReadOnlyList<int>? DependsOnSequences = null, IReadOnlyList<string>? AffectedAreas = null, string ChangeType = "Unknown", string Risk = "Unknown", string ConflictRisk = "Unknown", string? ExecutionReason = null, IReadOnlyList<string>? RepositoryEvidence = null, bool EstablishesSharedContract = false);
