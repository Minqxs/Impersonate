using Impersonate.Domain.Ai;
namespace Impersonate.Application.Planning;
public sealed record PlannerOptions { public string Provider{get;init;}="Anthropic"; public string Model{get;init;}=""; public string PromptVersion{get;init;}="planner-v2"; public int MaximumTasks{get;init;}=12; public int MaximumPlanningAttempts{get;init;}=2; public int MaximumOutputTokens{get;init;}=4000; public int TimeoutSeconds{get;init;}=45; public int PollIntervalSeconds{get;init;}=5; }
public sealed record PlannerAgentRequest(Guid ProjectId,string ProjectName,string? ProjectDescription,string RepositoryUrl,string DefaultBranch,string FeatureRequest,int MaximumTasks,string PromptVersion,string? CorrectionContext=null,Guid? ProviderConnectionId=null,ProviderType? RoutedProvider=null,string? RoutedModel=null,PlanningRepositoryContext? RepositoryContext=null);
public sealed record PlannerTask(int Sequence,string Title,string Description,IReadOnlyList<string> AcceptanceCriteria,IReadOnlyList<int>? DependsOnSequences=null,IReadOnlyList<string>? AffectedAreas=null,string ChangeType="Unknown",string Risk="Unknown",string ConflictRisk="Unknown",string? ExecutionReason=null,IReadOnlyList<string>? RepositoryEvidence=null,bool EstablishesSharedContract=false);
public sealed record PlannerPlan(string Summary,bool CanPlan,IReadOnlyList<string> PlanningNotes,IReadOnlyList<PlannerTask> Tasks,string? FailureReason,string? ClarifyingQuestion);
public sealed record PlannerAgentResult(PlannerPlan Plan,string? ProviderRequestId,int? InputTokenCount,int? OutputTokenCount);
public sealed record PlannerReadiness(string Status,bool ProviderConfigured,bool ModelConfigured,bool CredentialsConfigured,string Message)
{
 public bool IsReady => Status == "Ready";
}
public interface IPlannerReadiness { PlannerReadiness Get(); }
public interface IPlannerAgent { Task<PlannerAgentResult> PlanAsync(PlannerAgentRequest request,CancellationToken cancellationToken); }
public sealed record LanguageModelRequest(string Model,string SystemInstructions,string UserContent,string JsonSchema,int MaximumOutputTokens);
public sealed record LanguageModelResponse(string Content,string? ProviderRequestId,int? InputTokenCount,int? OutputTokenCount);
public interface ILanguageModelClient { Task<LanguageModelResponse> CompleteAsync(LanguageModelRequest request,CancellationToken cancellationToken); }
public sealed record PlanningRepositoryContext(IReadOnlyList<string> Tree,IReadOnlyList<string> RelevantFiles,IReadOnlyList<string> Languages,IReadOnlyList<string> Frameworks,IReadOnlyList<string> Layers,IReadOnlyList<string> TestLocations,IReadOnlyList<string> MigrationLocations,string Summary,string? ArtifactReference,IReadOnlySet<string> EvidencePaths);
public sealed record PlanningRepositoryContextResult(bool Succeeded,PlanningRepositoryContext? Context,string? FailureCode,string? FailureMessage);
public interface IPlanningRepositoryContextService { Task<PlanningRepositoryContextResult> BuildAsync(Guid projectId,Guid pipelineRunId,string repositoryUrl,string defaultBranch,string featureRequest,CancellationToken cancellationToken); }
public sealed record OrderedPlannerTask(PlannerTask Task,int OriginalSequence,int ExecutionSequence,bool OrderAdjusted,string? AdjustmentReason);
public sealed record ExecutionOrderResult(bool Succeeded,IReadOnlyList<OrderedPlannerTask> Tasks,IReadOnlyList<string> Errors);
public interface IExecutionOrderService { ExecutionOrderResult Order(IReadOnlyList<PlannerTask> tasks); }
public static class PlannerPlanValidator
{
 private static readonly string[] UnsupportedClaims = ["inspected the repository", "searched the repository", "examined the codebase", "ran the tests"];
 private static readonly string[] Placeholders = ["todo", "tbd", "placeholder", "fill in later"];
 public static IReadOnlyList<string> Validate(PlannerPlan plan,int max,IReadOnlySet<string>? evidencePaths=null)
 {
  var e=new List<string>();var tasks=plan.Tasks??[];
  if(string.IsNullOrWhiteSpace(plan.Summary))e.Add("Plan summary is required.");
  if(!plan.CanPlan){if(string.IsNullOrWhiteSpace(plan.FailureReason))e.Add("Failure reason is required.");if(string.IsNullOrWhiteSpace(plan.ClarifyingQuestion))e.Add("Clarifying question is required.");if(tasks.Count>0)e.Add("Unplannable responses cannot contain tasks.");return e;}
  if(tasks.Count is 0)e.Add("At least one task is required.");if(tasks.Count>max)e.Add($"Maximum task count is {max}.");
  if(!tasks.Select(x=>x.Sequence).SequenceEqual(Enumerable.Range(1,tasks.Count)))e.Add("Sequences must be contiguous from 1.");
  if(tasks.Any(x=>string.IsNullOrWhiteSpace(x.Title))||tasks.Where(x=>!string.IsNullOrWhiteSpace(x.Title)).GroupBy(x=>x.Title.Trim(),StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))e.Add("Task titles must be present and unique.");
  if(tasks.Any(x=>string.IsNullOrWhiteSpace(x.Title)||x.Title.Length>200||string.IsNullOrWhiteSpace(x.Description)||x.Description.Length>4000))e.Add("Task title or description is invalid.");
  if(tasks.Any(x=>x.AcceptanceCriteria is null||x.AcceptanceCriteria.Count==0||x.AcceptanceCriteria.Any(c=>string.IsNullOrWhiteSpace(c)||c.Length>500)))e.Add("Acceptance criteria are required and limited to 500 characters.");
  var sequences=tasks.Select(x=>x.Sequence).ToHashSet();
  var extended=tasks.Any(x=>x.DependsOnSequences is not null||x.AffectedAreas is not null||x.RepositoryEvidence is not null||!x.ChangeType.Equals("Unknown",StringComparison.OrdinalIgnoreCase));foreach(var task in tasks){var dependencies=task.DependsOnSequences??[];if(dependencies.Any(x=>x==task.Sequence))e.Add($"Task {task.Sequence} cannot depend on itself.");if(dependencies.Any(x=>!sequences.Contains(x)))e.Add($"Task {task.Sequence} references a missing dependency.");if(extended&&task.Sequence>1&&string.IsNullOrWhiteSpace(task.ExecutionReason))e.Add($"Task {task.Sequence} requires an execution-order reason.");var invalid=(task.RepositoryEvidence??[]).Where(path=>evidencePaths is null||!evidencePaths.Contains(Normalize(path))).Take(5).ToList();if(invalid.Count>0)e.Add($"Task {task.Sequence} contains repository evidence outside the planning snapshot: {string.Join(", ",invalid.Select(x=>$"'{x}'"))}. Use exact allowedRepositoryEvidencePaths values or an empty array.");}
  if(HasCycle(tasks))e.Add("Task dependency graph contains a cycle.");
  var text=string.Join(' ',tasks.Select(x=>$"{x.Title} {x.Description} {string.Join(' ',x.AcceptanceCriteria??[])}"));
  if(Placeholders.Any(x=>text.Contains(x,StringComparison.OrdinalIgnoreCase)))e.Add("Placeholder wording is not allowed.");
  if(UnsupportedClaims.Any(x=>text.Contains(x,StringComparison.OrdinalIgnoreCase)))e.Add("Planner output cannot claim repository inspection or execution.");
  return e;
 }
 private static bool HasCycle(IReadOnlyList<PlannerTask> tasks){var map=tasks.ToDictionary(x=>x.Sequence);var visiting=new HashSet<int>();var visited=new HashSet<int>();bool Visit(int sequence){if(visited.Contains(sequence))return false;if(!visiting.Add(sequence))return true;foreach(var dependency in map[sequence].DependsOnSequences??[])if(map.ContainsKey(dependency)&&Visit(dependency))return true;visiting.Remove(sequence);visited.Add(sequence);return false;}return tasks.Any(x=>Visit(x.Sequence));}
 private static string Normalize(string path)=>path.Replace('\\','/').TrimStart('/');
}
