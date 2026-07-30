using Impersonate.Application;
using Impersonate.Application.Planning;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Impersonate.Application.Tests;

public sealed class PlannerPlanValidatorTests
{
    [Fact]
    public void Accepts_ordered_bounded_plan()
    {
        var plan = new PlannerPlan("Summary", true, [], [new(1, "Add domain state", "Add the state transition.", ["Invalid transitions are rejected."]), new(2, "Expose endpoint", "Add the project-scoped endpoint.", ["The endpoint returns an accepted response."])], null, null);
        Assert.Empty(PlannerPlanValidator.Validate(plan, 12));
    }
    [Fact]
    public void Rejects_non_contiguous_sequences()
    {
        var plan = new PlannerPlan("Summary", true, [], [new(2, "Task", "Description", ["Criterion"])], null, null);
        Assert.Contains(PlannerPlanValidator.Validate(plan, 12), x => x.Contains("Sequences"));
    }
    [Fact]
    public void Requires_clarification_details()
    {
        var plan = new PlannerPlan("Summary", false, [], [], null, null);
        Assert.Equal(2, PlannerPlanValidator.Validate(plan, 12).Count);
    }
    [Fact]
    public void Rejects_duplicate_titles_and_task_limit()
    {
        var plan = new PlannerPlan("Summary", true, [], [new(1, "Same", "First", ["One"]), new(2, "same", "Second", ["Two"])], null, null);
        var errors = PlannerPlanValidator.Validate(plan, 1);
        Assert.Contains(errors, x => x.Contains("Maximum"));
        Assert.Contains(errors, x => x.Contains("unique"));
    }
    [Fact]
    public void Rejects_missing_summary_placeholders_and_execution_claims()
    {
        var plan = new PlannerPlan("", true, [], [new(1, "TODO endpoint", "I inspected the repository and ran the tests.", ["TBD"])], null, null);
        var errors = PlannerPlanValidator.Validate(plan, 12);
        Assert.Contains(errors, x => x.Contains("summary"));
        Assert.Contains(errors, x => x.Contains("Placeholder"));
        Assert.Contains(errors, x => x.Contains("inspection"));
    }
    [Fact]
    public void Rejects_missing_acceptance_criteria()
    {
        var plan = new PlannerPlan("Summary", true, [], [new(1, "Add endpoint", "Add the endpoint.", [])], null, null);
        Assert.Contains(PlannerPlanValidator.Validate(plan, 12), x => x.Contains("Acceptance criteria"));
    }
    [Fact]
    public void Rejects_dependency_cycles()
    {
        var tasks = new[] { new PlannerTask(1, "Contract", "Contract", ["Done"], [2], ["Domain"], "DomainModel", "Moderate", "Low", "First contract", [], true), new PlannerTask(2, "Consumer", "Consumer", ["Done"], [1], ["Api"], "ApiEndpoint", "Moderate", "Low", "Consumes contract", [], false) };
        var errors = PlannerPlanValidator.Validate(new("Summary", true, [], tasks, null, null), 12, new HashSet<string>());
        Assert.Contains(errors, x => x.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public void Rejects_fabricated_repository_evidence()
    {
        var task = new PlannerTask(1, "Change domain", "Change domain", ["Done"], [], ["Domain"], "DomainModel", "Moderate", "Low", "Establish contract", ["src/missing.cs"], true);
        var errors = PlannerPlanValidator.Validate(new("Summary", true, [], [task], null, null), 12, new HashSet<string> { "src/existing.cs" });
        Assert.Contains(errors, x => x.Contains("Task 1 repository evidence 'src/missing.cs' is not present"));
        Assert.DoesNotContain(errors, x => x.Contains(Directory.GetCurrentDirectory()));
    }
    [Fact]
    public void Evidence_diagnostics_are_bounded_and_hide_absolute_paths()
    {
        var paths = Enumerable.Range(0, 30).Select(x => x == 0 ? Path.GetFullPath("secret.cs") : $"src/invented-{x}-{new string('x', 300)}.cs").ToList();
        var task = new PlannerTask(1, "Change domain", "Change domain", ["Done"], RepositoryEvidence: paths);
        var errors = PlannerPlanValidator.Analyze(new("Summary", true, [], [task], null, null), 12, new HashSet<string>());
        Assert.True(errors.Count <= 10);
        Assert.True(errors.Sum(x => x.Message.Length) <= 2000);
        Assert.Contains(errors, x => x.OffendingPath == "invalid-relative-path");
        Assert.DoesNotContain(errors, x => x.Message.Contains(Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public void Exact_empty_and_canonical_evidence_are_safe()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "src/example/CustomerService.cs" };
        PlannerPlan Plan(params string[] evidence) => new("Summary", true, [], [new(1, "Change customer", "Change customer behavior", ["Behavior is covered"], [], ["Application"], "Service", "Moderate", "Low", "Establish contract", evidence, true)], null, null);
        Assert.Empty(PlannerPlanValidator.Validate(Plan("src/example/CustomerService.cs"), 12, allowed));
        Assert.Empty(PlannerPlanValidator.Validate(Plan(), 12, allowed));
        Assert.Equal(["src/example/CustomerService.cs"], PlannerEvidenceSanitizer.Sanitize(Plan("./src\\example\\CustomerService.cs", "src/example/CustomerService.cs"), allowed).Plan.Tasks[0].RepositoryEvidence);
    }
    [Fact]
    public void Directories_and_invented_paths_are_not_fuzzy_matched()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "src/customer/CustomerService.cs" };
        var plan = new PlannerPlan("Summary", true, [], [new(1, "Customer", "Implement customer behavior", ["Done"], [], [], "Service", "Moderate", "Low", "First", ["src/customer", "CustomerService.cs"], false)], null, null);
        var result = PlannerEvidenceSanitizer.Sanitize(plan, allowed);
        Assert.Empty(result.Plan.Tasks[0].RepositoryEvidence!);
        Assert.Equal(2, result.UnsupportedEvidence.Count);
    }
    [Fact]
    public void Correction_payload_contains_prior_plan_errors_and_allowed_paths()
    {
        var allowed = new HashSet<string> { "src/customer/CustomerService.cs", "tests/CustomerTests.cs" };
        var plan = new PlannerPlan("Summary", true, [], [new(1, "Customer", "Implement customer behavior", ["Done"], [], [], "Service", "Moderate", "Low", "First", ["invented.cs"], false)], null, null);
        var errors = PlannerEvidenceSanitizer.Sanitize(plan, allowed).UnsupportedEvidence;
        var correction = PlannerEvidenceSanitizer.BuildCorrection(errors, plan, allowed);
        Assert.Equal("Customer", correction.PreviousPlan.Tasks[0].Title);
        Assert.Contains(correction.AllowedRepositoryEvidencePaths, x => x == "src/customer/CustomerService.cs");
        Assert.Contains(correction.ValidationErrors, x => x.OffendingPath == "invented.cs" && x.Message.Contains("use []"));
    }
    [Fact]
    public void Request_payload_names_evidence_contract_and_includes_excerpts_and_correction()
    {
        var allowed = new HashSet<string> { "src/customer/CustomerService.cs" };
        var plan = new PlannerPlan("Summary", true, [], [new(1, "Customer", "Implement customer behavior", ["Done"], RepositoryEvidence: ["invented.cs"])], null, null);
        var correction = PlannerEvidenceSanitizer.BuildCorrection(PlannerEvidenceSanitizer.Sanitize(plan, allowed).UnsupportedEvidence, plan, allowed);
        var repository = new PlanningRepositoryContext(["src/customer/CustomerService.cs", "tests/Customer.Tests/Customer.Tests.csproj", "Project.sln"], [new("src/customer/CustomerService.cs", "class CustomerService {}", false)], [], [], [], ["tests/Customer.Tests"], [], "Snapshot", "artifact", allowed, ["Project.sln"], [new("tests/Customer.Tests/Customer.Tests.csproj", ["../../src/Customer.csproj"], ["Microsoft.NET.Test.Sdk"], true, true, false)], "TestProjectOutsideRelevantExcerpts");
        var payload = PlannerRequestPayload.Build(new(Guid.NewGuid(), "Project", null, "https://github.com/example/repo.git", "main", "Create customer management feature", 12, "planner-v2", correction, RepositoryContext: repository));
        using var json = System.Text.Json.JsonDocument.Parse(payload);
        var root = json.RootElement;
        Assert.Equal("src/customer/CustomerService.cs", root.GetProperty("allowedRepositoryEvidencePaths")[0].GetString());
        Assert.Equal("class CustomerService {}", root.GetProperty("repositoryContext").GetProperty("relevantFiles")[0].GetProperty("content").GetString());
        Assert.Equal("Project.sln", root.GetProperty("repositoryContext").GetProperty("solutionPaths")[0].GetString());
        Assert.Equal("TestProjectOutsideRelevantExcerpts", root.GetProperty("repositoryContext").GetProperty("testProjectEvidence").GetString());
        Assert.False(root.GetProperty("repositoryContext").GetProperty("projects")[0].GetProperty("includedInRelevantExcerpts").GetBoolean());
        Assert.Equal("Summary", root.GetProperty("correctionContext").GetProperty("previousPlan").GetProperty("summary").GetString());
    }
    [Fact]
    public void Only_evidence_errors_are_fallback_eligible_but_structural_errors_are_not()
    {
        var evidence = new[] { new PlannerValidationError("unsupported_repository_evidence", "bad") };
        Assert.True(PlannerEvidenceSanitizer.OnlyEvidenceErrors(evidence));
        Assert.False(PlannerEvidenceSanitizer.OnlyEvidenceErrors([.. evidence, new("dependency_cycle", "cycle")]));
        Assert.False(PlannerEvidenceSanitizer.OnlyEvidenceErrors([new("invalid_sequence", "sequence")]));
    }
    [Fact]
    public void Snapshot_ranking_is_deterministic_relevance_first_and_sensitive_safe()
    {
        var paths = Enumerable.Range(0, 600).Select(x => $"misc/{x:D3}.txt").Concat(["src/customer/CustomerService.cs", "Impersonate.sln", "src/Domain/Order.cs", ".env", "config/secrets.json"]).Reverse().ToList();
        var first = RepositoryEvidencePathPolicy.Rank(paths, "Create customer management feature");
        var second = RepositoryEvidencePathPolicy.Rank(paths.OrderDescending(), "Create customer management feature");
        Assert.Equal(first, second);
        Assert.Contains("src/customer/CustomerService.cs", first);
        Assert.DoesNotContain(".env", first);
        Assert.DoesNotContain("config/secrets.json", first);
        Assert.DoesNotContain(first, Path.IsPathRooted);
    }
    [Fact]
    public void Broad_customer_request_can_plan_without_fabricated_evidence()
    {
        var plan = new PlannerPlan("Customer management", true, ["Repository support is unknown."], [new(1, "Define customer management", "Add the bounded customer capability", ["The capability is defined"], [], ["Unknown"], "Unknown", "Unknown", "Unknown", "First task", [], false)], null, null);
        Assert.Empty(PlannerPlanValidator.Validate(plan, 12, new HashSet<string>()));
    }
    [Fact]
    public void Deterministic_order_places_shared_contract_before_independent_consumer()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddApplication().BuildServiceProvider();
        var order = services.GetRequiredService<IExecutionOrderService>();
        var tasks = new[] { new PlannerTask(1, "UI", "UI", ["Done"], [], ["FrontendUi"], "FrontendUi", "Low", "Low", "After contract", [], false), new PlannerTask(2, "Contract", "Contract", ["Done"], [], ["Domain"], "DomainModel", "Moderate", "High", "Shared contract", [], true) };
        var result = order.Order(tasks);
        Assert.True(result.Succeeded);
        Assert.Equal("Contract", result.Tasks[0].Task.Title);
        Assert.All(result.Tasks, x => Assert.True(x.OrderAdjusted));
    }
}
