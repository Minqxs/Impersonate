using Impersonate.Domain.Ai;
namespace Impersonate.Application.Planning;
public sealed record PlannerOptions { public string Provider{get;init;}="Anthropic"; public string Model{get;init;}=""; public string PromptVersion{get;init;}="planner-v1"; public int MaximumTasks{get;init;}=12; public int MaximumPlanningAttempts{get;init;}=2; public int TimeoutSeconds{get;init;}=120; public int PollIntervalSeconds{get;init;}=5; }
public sealed record PlannerAgentRequest(Guid ProjectId,string ProjectName,string? ProjectDescription,string RepositoryUrl,string DefaultBranch,string FeatureRequest,int MaximumTasks,string PromptVersion,string? CorrectionContext=null,Guid? ProviderConnectionId=null,ProviderType? RoutedProvider=null,string? RoutedModel=null);
public sealed record PlannerTask(int Sequence,string Title,string Description,IReadOnlyList<string> AcceptanceCriteria);
public sealed record PlannerPlan(string Summary,bool CanPlan,IReadOnlyList<string> PlanningNotes,IReadOnlyList<PlannerTask> Tasks,string? FailureReason,string? ClarifyingQuestion);
public sealed record PlannerAgentResult(PlannerPlan Plan,string? ProviderRequestId,int? InputTokenCount,int? OutputTokenCount);
public sealed record PlannerReadiness(string Status,bool ProviderConfigured,bool ModelConfigured,bool CredentialsConfigured,string Message)
{
 public bool IsReady => Status == "Ready";
}
public interface IPlannerReadiness { PlannerReadiness Get(); }
public interface IPlannerAgent { Task<PlannerAgentResult> PlanAsync(PlannerAgentRequest request,CancellationToken cancellationToken); }
public sealed record LanguageModelRequest(string Model,string SystemInstructions,string UserContent,string JsonSchema);
public sealed record LanguageModelResponse(string Content,string? ProviderRequestId,int? InputTokenCount,int? OutputTokenCount);
public interface ILanguageModelClient { Task<LanguageModelResponse> CompleteAsync(LanguageModelRequest request,CancellationToken cancellationToken); }
public static class PlannerPlanValidator
{
 private static readonly string[] UnsupportedClaims = ["inspected the repository", "searched the repository", "examined the codebase", "ran the tests"];
 private static readonly string[] Placeholders = ["todo", "tbd", "placeholder", "fill in later"];
 public static IReadOnlyList<string> Validate(PlannerPlan plan,int max)
 {
  var e=new List<string>();var tasks=plan.Tasks??[];
  if(string.IsNullOrWhiteSpace(plan.Summary))e.Add("Plan summary is required.");
  if(!plan.CanPlan){if(string.IsNullOrWhiteSpace(plan.FailureReason))e.Add("Failure reason is required.");if(string.IsNullOrWhiteSpace(plan.ClarifyingQuestion))e.Add("Clarifying question is required.");if(tasks.Count>0)e.Add("Unplannable responses cannot contain tasks.");return e;}
  if(tasks.Count is 0)e.Add("At least one task is required.");if(tasks.Count>max)e.Add($"Maximum task count is {max}.");
  if(!tasks.Select(x=>x.Sequence).SequenceEqual(Enumerable.Range(1,tasks.Count)))e.Add("Sequences must be contiguous from 1.");
  if(tasks.Any(x=>string.IsNullOrWhiteSpace(x.Title))||tasks.Where(x=>!string.IsNullOrWhiteSpace(x.Title)).GroupBy(x=>x.Title.Trim(),StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))e.Add("Task titles must be present and unique.");
  if(tasks.Any(x=>string.IsNullOrWhiteSpace(x.Title)||x.Title.Length>200||string.IsNullOrWhiteSpace(x.Description)||x.Description.Length>4000))e.Add("Task title or description is invalid.");
  if(tasks.Any(x=>x.AcceptanceCriteria is null||x.AcceptanceCriteria.Count==0||x.AcceptanceCriteria.Any(c=>string.IsNullOrWhiteSpace(c)||c.Length>500)))e.Add("Acceptance criteria are required and limited to 500 characters.");
  var text=string.Join(' ',tasks.Select(x=>$"{x.Title} {x.Description} {string.Join(' ',x.AcceptanceCriteria??[])}"));
  if(Placeholders.Any(x=>text.Contains(x,StringComparison.OrdinalIgnoreCase)))e.Add("Placeholder wording is not allowed.");
  if(UnsupportedClaims.Any(x=>text.Contains(x,StringComparison.OrdinalIgnoreCase)))e.Add("Planner output cannot claim repository inspection or execution.");
  return e;
 }
}
