using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public static class PlannerPlanValidator
{
    private static readonly string[] UnsupportedClaims = ["inspected the repository", "searched the repository", "examined the codebase", "ran the tests"];
    private static readonly string[] Placeholders = ["todo", "tbd", "placeholder", "fill in later"];
    public static IReadOnlyList<string> Validate(PlannerPlan plan, int max, IReadOnlySet<string>? evidencePaths = null) => Analyze(plan, max, evidencePaths).Select(x => x.Message).ToList();
    public static IReadOnlyList<PlannerValidationError> Analyze(PlannerPlan plan, int max, IReadOnlySet<string>? evidencePaths = null)
    {
        var e = new List<PlannerValidationError>();
        var tasks = plan.Tasks ?? [];
        void Add(string code, string message, int? task = null, string? path = null) => e.Add(new(code, message, task, path));
        if (string.IsNullOrWhiteSpace(plan.Summary))
            Add("missing_summary", "Plan summary is required.");
        if (!plan.CanPlan)
        {
            if (string.IsNullOrWhiteSpace(plan.FailureReason))
                Add("missing_failure_reason", "Failure reason is required.");
            if (string.IsNullOrWhiteSpace(plan.ClarifyingQuestion))
                Add("missing_clarifying_question", "Clarifying question is required.");
            if (tasks.Count > 0)
                Add("unplannable_has_tasks", "Unplannable responses cannot contain tasks.");
            return Bound(e);
        }

        if (tasks.Count is 0)
            Add("missing_tasks", "At least one task is required.");
        if (tasks.Count > max)
            Add("task_limit", $"Maximum task count is {max}.");
        if (!tasks.Select(x => x.Sequence).SequenceEqual(Enumerable.Range(1, tasks.Count)))
            Add("invalid_sequence", "Sequences must be contiguous from 1.");
        if (tasks.Any(x => string.IsNullOrWhiteSpace(x.Title)) || tasks.Where(x => !string.IsNullOrWhiteSpace(x.Title)).GroupBy(x => x.Title.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            Add("invalid_title", "Task titles must be present and unique.");
        if (tasks.Any(x => string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 200 || string.IsNullOrWhiteSpace(x.Description) || x.Description.Length > 4000))
            Add("invalid_task_content", "Task title or description is invalid.");
        if (tasks.Any(x => x.AcceptanceCriteria is null || x.AcceptanceCriteria.Count == 0 || x.AcceptanceCriteria.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 500)))
            Add("invalid_acceptance_criteria", "Acceptance criteria are required and limited to 500 characters.");
        var sequences = tasks.Select(x => x.Sequence).ToHashSet();
        var extended = tasks.Any(x => x.DependsOnSequences is not null || x.AffectedAreas is not null || x.RepositoryEvidence is not null || !x.ChangeType.Equals("Unknown", StringComparison.OrdinalIgnoreCase));
        foreach (var task in tasks)
        {
            var dependencies = task.DependsOnSequences ?? [];
            if (dependencies.Any(x => x == task.Sequence))
                Add("self_dependency", $"Task {task.Sequence} cannot depend on itself.", task.Sequence);
            if (dependencies.Any(x => !sequences.Contains(x)))
                Add("missing_dependency", $"Task {task.Sequence} references a missing dependency.", task.Sequence);
            if (extended && task.Sequence > 1 && string.IsNullOrWhiteSpace(task.ExecutionReason))
                Add("missing_execution_reason", $"Task {task.Sequence} requires an execution-order reason.", task.Sequence);
            foreach (var path in (task.RepositoryEvidence ?? []).Where(path => evidencePaths is null || !evidencePaths.Contains(Normalize(path))).Take(5))
            {
                var safe = SafePath(path);
                Add("unsupported_repository_evidence", $"Task {task.Sequence} repository evidence '{safe}' is not present in the planning snapshot.", task.Sequence, safe);
            }
        }

        if (HasCycle(tasks))
            Add("dependency_cycle", "Task dependency graph contains a cycle.");
        var text = string.Join(' ', tasks.Select(x => $"{x.Title} {x.Description} {string.Join(' ', x.AcceptanceCriteria ?? [])}"));
        if (Placeholders.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase)))
            Add("placeholder", "Placeholder wording is not allowed.");
        if (UnsupportedClaims.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase)))
            Add("false_execution_claim", "Planner output cannot claim repository inspection or execution.");
        return Bound(e);
    }

    private static IReadOnlyList<PlannerValidationError> Bound(IEnumerable<PlannerValidationError> errors)
    {
        const int maximumCount = 10, maximumCharacters = 2000;
        var result = new List<PlannerValidationError>();
        var used = 0;
        foreach (var error in errors.Take(maximumCount))
        {
            var remaining = maximumCharacters - used;
            if (remaining <= 0)
                break;
            var message = error.Message.Length <= remaining ? error.Message : error.Message[..remaining];
            result.Add(error with
            {
                Message = message
            });
            used += message.Length;
        }

        return result;
    }

    private static bool HasCycle(IReadOnlyList<PlannerTask> tasks)
    {
        var map = tasks.ToDictionary(x => x.Sequence);
        var visiting = new HashSet<int>();
        var visited = new HashSet<int>();
        bool Visit(int sequence)
        {
            if (visited.Contains(sequence))
                return false;
            if (!visiting.Add(sequence))
                return true;
            foreach (var dependency in map[sequence].DependsOnSequences ?? [])
                if (map.ContainsKey(dependency) && Visit(dependency))
                    return true;
            visiting.Remove(sequence);
            visited.Add(sequence);
            return false;
        }

        return tasks.Any(x => Visit(x.Sequence));
    }

    internal static string Normalize(string path)
    {
        var value = (path ?? string.Empty).Trim().Replace('\\', '/');
        while (value.StartsWith("./", StringComparison.Ordinal))
            value = value[2..];
        return value.TrimStart('/');
    }

    private static string SafePath(string path)
    {
        var value = Normalize(path);
        if (Path.IsPathRooted(path) || value.Contains("../", StringComparison.Ordinal) || value == "..")
            return "invalid-relative-path";
        return value.Length <= 240 ? value : value[..237] + "...";
    }
}
