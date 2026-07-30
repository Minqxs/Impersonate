using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlannerCorrectionContext(IReadOnlyList<PlannerValidationError> ValidationErrors, PlannerPlan PreviousPlan, IReadOnlyList<string> AllowedRepositoryEvidencePaths);
