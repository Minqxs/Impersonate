using System.Text.Json;
using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

internal sealed class DeterministicTaskProfiler : ITaskProfiler
{
    private static readonly string[] HighSignals = ["architecture", "migration", "security", "concurrency", "redesign", "integration"];
    public TaskProfile Profile(AgentRole role, string description) => Profile(new(Guid.Empty, null, role, description));
    public TaskProfile Profile(ModelSelectionRequest request)
    {
        var taskText = string.Join(' ', new[] { request.TaskTitle, request.Description, request.ChangeType, string.Join(' ', request.AcceptanceCriteria ?? []), string.Join(' ', request.AffectedAreas ?? []), string.Join(' ', request.RepositoryEvidence ?? []), request.ReviewerFeedback }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var security = taskText.Contains("security", StringComparison.OrdinalIgnoreCase) || taskText.Contains("credential", StringComparison.OrdinalIgnoreCase) || taskText.Contains("authenticated", StringComparison.OrdinalIgnoreCase) || taskText.Contains("authorization", StringComparison.OrdinalIgnoreCase);
        var concurrency = taskText.Contains("concurr", StringComparison.OrdinalIgnoreCase) || taskText.Contains("claim", StringComparison.OrdinalIgnoreCase) || taskText.Contains("transaction", StringComparison.OrdinalIgnoreCase);
        var database = taskText.Contains("migration", StringComparison.OrdinalIgnoreCase) || taskText.Contains("database", StringComparison.OrdinalIgnoreCase) || taskText.Contains("persistence", StringComparison.OrdinalIgnoreCase);
        var architecture = HighSignals.Any(x => taskText.Contains(x, StringComparison.OrdinalIgnoreCase));
        var high = taskText.Length > 1200 || security || concurrency || request.Risk.Equals("High", StringComparison.OrdinalIgnoreCase) || request.ConflictRisk.Equals("High", StringComparison.OrdinalIgnoreCase) || request.RevisionCount > 0 && !string.IsNullOrWhiteSpace(request.ReviewerFeedback);
        var simple = taskText.Length < 400 && !high && !database && !architecture && request.ExpectedFileCount <= 1;
        var complexity = high ? TaskComplexity.High : simple ? TaskComplexity.Simple : TaskComplexity.Moderate;
        var type = TaskType(request.ChangeType, taskText);
        var reasons = new List<string>
        {
            $"Profiled as {type} for the {request.Role} role.",
            high ? "Task evidence indicates high risk, architecture, security, concurrency, or revision demand." : simple ? "The current task is bounded, one-file, and low-risk." : "The current task has moderate scope and evidence."
        };
        return new(request.Role, complexity, high ? RiskLevel.High : simple ? RiskLevel.Low : RiskLevel.Moderate, request.Role == AgentRole.Coder, true, request.Role is AgentRole.Planner or AgentRole.Reviewer, request.Role == AgentRole.Coder, Math.Max(2000, taskText.Length * 3), high ? Sensitivity.Low : Sensitivity.Balanced, simple ? Sensitivity.High : Sensitivity.Balanced, reasons, type, request.RepositoryLanguages ?? [], request.RepositoryFrameworks ?? [], request.AffectedAreas ?? [], request.ChangeType, request.ConflictRisk, database, security, concurrency, architecture, request.AttemptNumber, request.PriorFailures, request.RevisionCount, request.ReviewerFeedback, request.ExpectedDiffSize > 0 ? request.ExpectedDiffSize : Math.Max(500, taskText.Length * 2));
    }

    private static EngineeringTaskType TaskType(string changeType, string text)
    {
        var value = changeType + " " + text;
        foreach (var candidate in Enum.GetValues<EngineeringTaskType>().Where(x => x != EngineeringTaskType.Unknown))
            if (value.Contains(candidate.ToString(), StringComparison.OrdinalIgnoreCase))
                return candidate;
        if (value.Contains("migration", StringComparison.OrdinalIgnoreCase))
            return EngineeringTaskType.DatabaseMigration;
        if (value.Contains("endpoint", StringComparison.OrdinalIgnoreCase) || value.Contains(" api ", StringComparison.OrdinalIgnoreCase))
            return EngineeringTaskType.ApiEndpoint;
        if (value.Contains("frontend", StringComparison.OrdinalIgnoreCase) || value.Contains(" ui ", StringComparison.OrdinalIgnoreCase))
            return EngineeringTaskType.FrontendUi;
        if (value.Contains("test", StringComparison.OrdinalIgnoreCase))
            return EngineeringTaskType.Testing;
        return EngineeringTaskType.Unknown;
    }
}
