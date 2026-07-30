using System.Text.RegularExpressions;
using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

internal sealed class DeterministicTaskProfiler : ITaskProfiler
{
    private static readonly Regex DatabaseNegative = new(@"\bno\s+new\s+database\s+column\s+or\s+migration\b|\b(?:no|without|does\s+not\s+require|do\s+not\s+(?:add|create)|must\s+not\s+(?:add|create))\s+(?:an?\s+)?(?:new\s+)?(?:database\s+)?(?:columns?|changes?|migrations?|schema\s+changes?)\b|\b(computed\s+only|not\s+persisted|read[- ]only\s+projection)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DatabasePositive = new(@"\b(add|create|change|update|alter)\s+(an?\s+)?(ef\s+core\s+)?(migration|schema|persisted\s+column|database\s+(column|index))\b|\b(migrations?|persisted\s+column|database\s+index|ef\s+mapping|dbcontext\s+change|repository\s+persistence|persist(ed|ence)\s+(property|field|column))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public TaskProfile Profile(AgentRole role, string description) => Profile(new(Guid.Empty, null, role, description));

    public TaskProfile Profile(ModelSelectionRequest request)
    {
        var structured = new[] { request.ChangeType, string.Join(' ', request.AffectedAreas ?? []), string.Join(' ', request.AcceptanceCriteria ?? []), string.Join(' ', request.RepositoryEvidence ?? []) };
        var explicitTask = string.Join(' ', new[] { request.TaskTitle, request.Description }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var taskSources = structured.Append(explicitTask).ToList();
        var positiveDatabaseEvidence = PositiveDatabaseEvidence(taskSources);
        var negativeDatabaseEvidence = NegativeDatabaseEvidence(taskSources);
        if (positiveDatabaseEvidence.Count == 0 && negativeDatabaseEvidence.Count == 0)
        {
            positiveDatabaseEvidence = PositiveDatabaseEvidence([request.FeatureRequest]);
            negativeDatabaseEvidence = NegativeDatabaseEvidence([request.FeatureRequest]);
        }
        var database = positiveDatabaseEvidence.Count > 0;
        var taskText = string.Join(' ', structured.Append(explicitTask).Append(request.ReviewerFeedback).Where(x => !string.IsNullOrWhiteSpace(x)));
        var security = ContainsAny(taskText, "security", "credential", "authenticated", "authorization");
        var concurrency = ContainsAny(taskText, "concurr", "claim", "transaction");
        var architecture = ContainsAny(taskText, "architecture", "redesign", "integration") || database;
        var high = taskText.Length > 1200 || security || concurrency || request.Risk.Equals("High", StringComparison.OrdinalIgnoreCase) || request.ConflictRisk.Equals("High", StringComparison.OrdinalIgnoreCase) || request.RevisionCount > 0 && !string.IsNullOrWhiteSpace(request.ReviewerFeedback);
        var simple = taskText.Length < 400 && !high && !database && !architecture && request.ExpectedFileCount <= 1;
        var complexity = high ? TaskComplexity.High : simple ? TaskComplexity.Simple : TaskComplexity.Moderate;
        var type = TaskType(request, database);
        var reasons = new List<string>
        {
            $"Profiled as {type} for the {request.Role} role.",
            high ? "Task evidence indicates high risk, security, concurrency, or revision demand." : simple ? "The current task is bounded, one-file, and low-risk." : "The current task has moderate scope and evidence.",
            positiveDatabaseEvidence.Count == 0 ? "No independent positive database-change evidence was found." : "Positive database evidence: " + string.Join("; ", positiveDatabaseEvidence) + "."
        };
        if (negativeDatabaseEvidence.Count > 0)
            reasons.Add("Negative database constraints: " + string.Join("; ", negativeDatabaseEvidence) + ".");
        return new(request.Role, complexity, high ? RiskLevel.High : simple ? RiskLevel.Low : RiskLevel.Moderate, request.Role == AgentRole.Coder, true, request.Role is AgentRole.Planner or AgentRole.Reviewer, request.Role == AgentRole.Coder, Math.Max(2000, taskText.Length * 3), high ? Sensitivity.Low : Sensitivity.Balanced, simple ? Sensitivity.High : Sensitivity.Balanced, reasons, type, request.RepositoryLanguages ?? [], request.RepositoryFrameworks ?? [], request.AffectedAreas ?? [], request.ChangeType, request.ConflictRisk, database, security, concurrency, architecture, request.AttemptNumber, request.PriorFailures, request.RevisionCount, request.ReviewerFeedback, request.ExpectedDiffSize > 0 ? request.ExpectedDiffSize : Math.Max(500, taskText.Length * 2));
    }

    private static EngineeringTaskType TaskType(ModelSelectionRequest request, bool database)
    {
        var normalized = request.ChangeType.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<EngineeringTaskType>(normalized, true, out var structuredType) && structuredType != EngineeringTaskType.Unknown)
            return structuredType;
        if (database)
            return EngineeringTaskType.DatabaseMigration;
        if ((request.AffectedAreas ?? []).Any(x => ContainsWord(x, "api", "controller", "endpoint")) || ContainsWord(request.TaskTitle, "api", "endpoint"))
            return EngineeringTaskType.ApiEndpoint;
        if ((request.AffectedAreas ?? []).Any(x => ContainsWord(x, "frontend", "ui")))
            return EngineeringTaskType.FrontendUi;
        if ((request.AffectedAreas ?? []).Any(x => ContainsWord(x, "test", "tests", "testing", "qa")) || ContainsPhrase(request.TaskTitle, "add tests", "test project", "test coverage"))
            return EngineeringTaskType.Testing;
        return EngineeringTaskType.Unknown;
    }

    private static IReadOnlyList<string> PositiveDatabaseEvidence(IEnumerable<string?> sources)
    {
        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source))
                continue;
            var normalized = source.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (normalized.Equals("DatabaseMigration", StringComparison.OrdinalIgnoreCase) || source.Equals("Database", StringComparison.OrdinalIgnoreCase) || source.Equals("Persistence", StringComparison.OrdinalIgnoreCase) || source.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) || source.Contains("\\Migrations\\", StringComparison.OrdinalIgnoreCase))
                return [source.Trim()];
            var withoutNegatives = DatabaseNegative.Replace(source, string.Empty);
            var matches = DatabasePositive.Matches(withoutNegatives).Select(x => x.Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (matches.Count > 0)
                return matches;
        }
        return [];
    }

    private static IReadOnlyList<string> NegativeDatabaseEvidence(IEnumerable<string?> sources) => sources
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .SelectMany(x => DatabaseNegative.Matches(x!).Select(match => match.Value.Trim()))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static bool ContainsAny(string? value, params string[] terms) => !string.IsNullOrWhiteSpace(value) && terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    private static bool ContainsPhrase(string? value, params string[] phrases) => !string.IsNullOrWhiteSpace(value) && phrases.Any(phrase => value.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    private static bool ContainsWord(string? value, params string[] words) => !string.IsNullOrWhiteSpace(value) && words.Any(word => Regex.IsMatch(value, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
}
