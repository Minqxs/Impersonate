using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public static class PlannerEvidenceSanitizer
{
    public static PlannerEvidenceSanitization Sanitize(PlannerPlan plan, IReadOnlySet<string> allowed)
    {
        var canonical = allowed.ToDictionary(PlannerPlanValidator.Normalize, x => x, StringComparer.OrdinalIgnoreCase);
        var unsupported = new List<PlannerValidationError>();
        var tasks = (plan.Tasks ?? []).Select(task =>
        {
            var evidence = new List<string>();
            foreach (var raw in task.RepositoryEvidence ?? [])
            {
                var normalized = PlannerPlanValidator.Normalize(raw);
                if (canonical.TryGetValue(normalized, out var exact))
                {
                    if (!evidence.Contains(exact, StringComparer.OrdinalIgnoreCase))
                        evidence.Add(exact);
                }
                else
                {
                    var safe = Path.IsPathRooted(raw) || normalized.Contains("../", StringComparison.Ordinal) || normalized == ".." ? "invalid-relative-path" : normalized;
                    safe = safe.Length <= 240 ? safe : safe[..237] + "...";
                    unsupported.Add(new("unsupported_repository_evidence", $"Task {task.Sequence} repository evidence '{safe}' is not present in the planning snapshot.", task.Sequence, safe));
                }
            }

            return task with
            {
                RepositoryEvidence = evidence
            };
        }).ToList();
        return new(plan with
        {
            Tasks = tasks
        }, unsupported.Take(10).ToList());
    }

    public static bool OnlyEvidenceErrors(IReadOnlyList<PlannerValidationError> errors) => errors.Count > 0 && errors.All(x => x.Code == "unsupported_repository_evidence");
    public static PlannerCorrectionContext BuildCorrection(IReadOnlyList<PlannerValidationError> errors, PlannerPlan previous, IReadOnlySet<string> allowed)
    {
        var suitable = allowed.Order(StringComparer.Ordinal).Take(40).ToList();
        var bounded = errors.Take(10).Select(x => x.Code == "unsupported_repository_evidence" ? x with { Message = $"{x.Message} Remove it or replace it with an exact allowed path; use [] when none apply." } : x).ToList();
        var prior = previous with
        {
            Summary = Limit(previous.Summary, 1000),
            PlanningNotes = (previous.PlanningNotes ?? []).Take(10).Select(x => Limit(x, 500)).ToList(),
            Tasks = (previous.Tasks ?? []).Take(20).Select(x => x with { Title = Limit(x.Title, 200), Description = Limit(x.Description, 1000), AcceptanceCriteria = (x.AcceptanceCriteria ?? []).Take(10).Select(y => Limit(y, 500)).ToList() }).ToList()
        };
        return new(bounded, prior, suitable);
    }

    private static string Limit(string? value, int max) => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
