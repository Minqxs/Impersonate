using Impersonate.Application.Planning;
using Impersonate.Domain.Pipelines;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class PlannerEvidenceRecoveryTests
{
    [Fact]
    public void Targeted_second_attempt_receives_prior_plan_and_reaches_ready_without_invalid_evidence()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "src/customer/CustomerService.cs" };
        var first = Plan("src/customer/InventedService.cs");
        var second = Plan("src/customer/CustomerService.cs");

        var result = Execute([first, second], allowed);

        Assert.Equal(PipelineRunStatus.ReadyForExecution, result.Run.Status);
        Assert.Single(result.InvalidAttempts);
        Assert.NotNull(result.Corrections[0]);
        Assert.Equal("src/customer/InventedService.cs", result.Corrections[0]!.ValidationErrors[0].OffendingPath);
        Assert.Contains("src/customer/CustomerService.cs", result.Corrections[0]!.AllowedRepositoryEvidencePaths);
        Assert.Equal(first.Tasks[0].Title, result.Corrections[0]!.PreviousPlan.Tasks[0].Title);
        Assert.Equal(["src/customer/CustomerService.cs"], System.Text.Json.JsonSerializer.Deserialize<List<string>>(result.Run.Tasks[0].RepositoryEvidenceJson));
        Assert.DoesNotContain("InventedService", result.Run.Tasks[0].RepositoryEvidenceJson);
    }

    [Fact]
    public void Final_evidence_only_failure_is_stripped_warned_and_allowed_to_continue()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "src/customer/CustomerService.cs" };
        var result = Execute([Plan("invented-one.cs"), Plan("invented-two.cs")], allowed);

        Assert.Equal(PipelineRunStatus.ReadyForExecution, result.Run.Status);
        Assert.Single(result.InvalidAttempts);
        Assert.Empty(System.Text.Json.JsonSerializer.Deserialize<List<string>>(result.Run.Tasks[0].RepositoryEvidenceJson)!);
        Assert.Contains("discarded", result.Run.PlanningWarningsJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Run.Events, x => x.EventType == "PlanningEvidenceDiscarded");
    }

    private static RecoveryResult Execute(IReadOnlyList<PlannerPlan> responses, IReadOnlySet<string> allowed)
    {
        var run = PipelineRun.Create(Guid.NewGuid(), "Create customer management feature");
        run.StartPlanning();
        var corrections = new List<PlannerCorrectionContext?>();
        var invalid = new List<IReadOnlyList<PlannerValidationError>>();
        PlannerPlan? accepted = null;
        for (var index = 0; index < responses.Count; index++)
        {
            var raw = responses[index];
            var sanitized = PlannerEvidenceSanitizer.Sanitize(raw, allowed);
            var errors = PlannerPlanValidator.Analyze(sanitized.Plan, 12, allowed).Concat(sanitized.UnsupportedEvidence).ToList();
            if (errors.Count == 0) { accepted = sanitized.Plan; break; }
            if (index == responses.Count - 1 && PlannerEvidenceSanitizer.OnlyEvidenceErrors(errors))
            {
                run.RecordPlanningWarning("Some repository evidence proposed by the Planner was discarded because it was not present in the bounded snapshot.");
                accepted = sanitized.Plan;
                break;
            }
            invalid.Add(errors);
            var correction = PlannerEvidenceSanitizer.BuildCorrection(errors, raw, allowed);
            if (index + 1 < responses.Count) corrections.Add(correction);
        }
        Assert.NotNull(accepted);
        foreach (var candidate in accepted!.Tasks)
        {
            var task = run.AddTask(candidate.Sequence, candidate.Title, candidate.Description, candidate.AcceptanceCriteria);
            task.SetIntelligence([], candidate.AffectedAreas ?? [], candidate.ChangeType, candidate.Risk, candidate.ConflictRisk, candidate.ExecutionReason ?? "First task", candidate.RepositoryEvidence ?? [], candidate.Sequence, false, null, candidate.EstablishesSharedContract);
        }
        run.MarkReadyForExecution();
        return new(run, corrections.Where(x => x is not null).ToList(), invalid);
    }

    private static PlannerPlan Plan(string evidence) => new("Customer plan", true, [], [new(1, "Implement customer management", "Implement repository-supported customer management.", ["Customer management behavior is available."], [], ["Application"], "Service", "Moderate", "Low", "Establish the customer contract.", [evidence], true)], null, null);
    private sealed record RecoveryResult(PipelineRun Run, IReadOnlyList<PlannerCorrectionContext?> Corrections, IReadOnlyList<IReadOnlyList<PlannerValidationError>> InvalidAttempts);
}
