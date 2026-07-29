using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlannerEvidenceSanitization(PlannerPlan Plan, IReadOnlyList<PlannerValidationError> UnsupportedEvidence);
