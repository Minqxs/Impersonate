using Impersonate.Domain.Ai;
namespace Impersonate.Application.Planning;
public sealed record PlannerOptions { public string Provider{get;init;}="Anthropic"; public string Model{get;init;}=""; public string PromptVersion{get;init;}="planner-v2"; public int MaximumTasks{get;init;}=12; public int MaximumPlanningAttempts{get;init;}=2; public int MaximumOutputTokens{get;init;}=4000; public int TimeoutSeconds{get;init;}=45; public int PollIntervalSeconds{get;init;}=5; }
public sealed record PlannerAgentRequest(Guid ProjectId,string ProjectName,string? ProjectDescription,string RepositoryUrl,string DefaultBranch,string FeatureRequest,int MaximumTasks,string PromptVersion,PlannerCorrectionContext? CorrectionContext=null,Guid? ProviderConnectionId=null,ProviderType? RoutedProvider=null,string? RoutedModel=null,PlanningRepositoryContext? RepositoryContext=null);
public sealed record PlannerTask(int Sequence,string Title,string Description,IReadOnlyList<string> AcceptanceCriteria,IReadOnlyList<int>? DependsOnSequences=null,IReadOnlyList<string>? AffectedAreas=null,string ChangeType="Unknown",string Risk="Unknown",string ConflictRisk="Unknown",string? ExecutionReason=null,IReadOnlyList<string>? RepositoryEvidence=null,bool EstablishesSharedContract=false);
public sealed record PlannerPlan(string Summary,bool CanPlan,IReadOnlyList<string> PlanningNotes,IReadOnlyList<PlannerTask> Tasks,string? FailureReason,string? ClarifyingQuestion);
public sealed record PlannerAgentResult(PlannerPlan Plan,string? ProviderRequestId,int? InputTokenCount,int? OutputTokenCount);
public sealed record PlannerReadiness(string Status,bool ProviderConfigured,bool ModelConfigured,bool CredentialsConfigured,string Message)
{
 public bool IsReady => Status == "Ready";
}
public interface IPlannerReadiness { PlannerReadiness Get(); }
public interface IPlannerAgent { Task<PlannerAgentResult> PlanAsync(PlannerAgentRequest request,CancellationToken cancellationToken); }
public static class PlannerRequestPayload
{
 public static string Build(PlannerAgentRequest request)=>System.Text.Json.JsonSerializer.Serialize(new{project=new{request.ProjectName,request.ProjectDescription,request.DefaultBranch},request.FeatureRequest,constraints=new{request.MaximumTasks,repositoryInspectionAvailable=request.RepositoryContext is not null},allowedRepositoryEvidencePaths=request.RepositoryContext?.EvidencePaths.Order(StringComparer.Ordinal).ToList()??[],repositoryContext=request.RepositoryContext is null?null:new{request.RepositoryContext.Tree,request.RepositoryContext.RelevantFiles,request.RepositoryContext.Languages,request.RepositoryContext.Frameworks,request.RepositoryContext.Layers,request.RepositoryContext.TestLocations,request.RepositoryContext.MigrationLocations,request.RepositoryContext.Summary},correctionContext=request.CorrectionContext},new System.Text.Json.JsonSerializerOptions{PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.CamelCase});
}
public sealed record LanguageModelRequest(string Model,string SystemInstructions,string UserContent,string JsonSchema,int MaximumOutputTokens);
public sealed record LanguageModelResponse(string Content,string? ProviderRequestId,int? InputTokenCount,int? OutputTokenCount);
public interface ILanguageModelClient { Task<LanguageModelResponse> CompleteAsync(LanguageModelRequest request,CancellationToken cancellationToken); }
public sealed record PlanningRelevantFile(string Path,string Content,bool Truncated);
public sealed record PlanningRepositoryContext(IReadOnlyList<string> Tree,IReadOnlyList<PlanningRelevantFile> RelevantFiles,IReadOnlyList<string> Languages,IReadOnlyList<string> Frameworks,IReadOnlyList<string> Layers,IReadOnlyList<string> TestLocations,IReadOnlyList<string> MigrationLocations,string Summary,string? ArtifactReference,IReadOnlySet<string> EvidencePaths);
public sealed record PlannerValidationError(string Code,string Message,int? TaskSequence=null,string? OffendingPath=null);
public sealed record PlannerCorrectionContext(IReadOnlyList<PlannerValidationError> ValidationErrors,PlannerPlan PreviousPlan,IReadOnlyList<string> AllowedRepositoryEvidencePaths);
public sealed record PlannerEvidenceSanitization(PlannerPlan Plan,IReadOnlyList<PlannerValidationError> UnsupportedEvidence);
public sealed record PlanningRepositoryContextResult(bool Succeeded,PlanningRepositoryContext? Context,string? FailureCode,string? FailureMessage);
public interface IPlanningRepositoryContextService { Task<PlanningRepositoryContextResult> BuildAsync(Guid projectId,Guid pipelineRunId,string repositoryUrl,string defaultBranch,string featureRequest,CancellationToken cancellationToken); }
public sealed record OrderedPlannerTask(PlannerTask Task,int OriginalSequence,int ExecutionSequence,bool OrderAdjusted,string? AdjustmentReason);
public sealed record ExecutionOrderResult(bool Succeeded,IReadOnlyList<OrderedPlannerTask> Tasks,IReadOnlyList<string> Errors);
public interface IExecutionOrderService { ExecutionOrderResult Order(IReadOnlyList<PlannerTask> tasks); }
public static class PlannerPlanValidator
{
 private static readonly string[] UnsupportedClaims = ["inspected the repository", "searched the repository", "examined the codebase", "ran the tests"];
 private static readonly string[] Placeholders = ["todo", "tbd", "placeholder", "fill in later"];
 public static IReadOnlyList<string> Validate(PlannerPlan plan,int max,IReadOnlySet<string>? evidencePaths=null)=>Analyze(plan,max,evidencePaths).Select(x=>x.Message).ToList();
 public static IReadOnlyList<PlannerValidationError> Analyze(PlannerPlan plan,int max,IReadOnlySet<string>? evidencePaths=null)
 {
  var e=new List<PlannerValidationError>();var tasks=plan.Tasks??[];void Add(string code,string message,int? task=null,string? path=null)=>e.Add(new(code,message,task,path));
  if(string.IsNullOrWhiteSpace(plan.Summary))Add("missing_summary","Plan summary is required.");
  if(!plan.CanPlan){if(string.IsNullOrWhiteSpace(plan.FailureReason))Add("missing_failure_reason","Failure reason is required.");if(string.IsNullOrWhiteSpace(plan.ClarifyingQuestion))Add("missing_clarifying_question","Clarifying question is required.");if(tasks.Count>0)Add("unplannable_has_tasks","Unplannable responses cannot contain tasks.");return Bound(e);}
  if(tasks.Count is 0)Add("missing_tasks","At least one task is required.");if(tasks.Count>max)Add("task_limit",$"Maximum task count is {max}.");
  if(!tasks.Select(x=>x.Sequence).SequenceEqual(Enumerable.Range(1,tasks.Count)))Add("invalid_sequence","Sequences must be contiguous from 1.");
  if(tasks.Any(x=>string.IsNullOrWhiteSpace(x.Title))||tasks.Where(x=>!string.IsNullOrWhiteSpace(x.Title)).GroupBy(x=>x.Title.Trim(),StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))Add("invalid_title","Task titles must be present and unique.");
  if(tasks.Any(x=>string.IsNullOrWhiteSpace(x.Title)||x.Title.Length>200||string.IsNullOrWhiteSpace(x.Description)||x.Description.Length>4000))Add("invalid_task_content","Task title or description is invalid.");
  if(tasks.Any(x=>x.AcceptanceCriteria is null||x.AcceptanceCriteria.Count==0||x.AcceptanceCriteria.Any(c=>string.IsNullOrWhiteSpace(c)||c.Length>500)))Add("invalid_acceptance_criteria","Acceptance criteria are required and limited to 500 characters.");
  var sequences=tasks.Select(x=>x.Sequence).ToHashSet();
  var extended=tasks.Any(x=>x.DependsOnSequences is not null||x.AffectedAreas is not null||x.RepositoryEvidence is not null||!x.ChangeType.Equals("Unknown",StringComparison.OrdinalIgnoreCase));foreach(var task in tasks){var dependencies=task.DependsOnSequences??[];if(dependencies.Any(x=>x==task.Sequence))Add("self_dependency",$"Task {task.Sequence} cannot depend on itself.",task.Sequence);if(dependencies.Any(x=>!sequences.Contains(x)))Add("missing_dependency",$"Task {task.Sequence} references a missing dependency.",task.Sequence);if(extended&&task.Sequence>1&&string.IsNullOrWhiteSpace(task.ExecutionReason))Add("missing_execution_reason",$"Task {task.Sequence} requires an execution-order reason.",task.Sequence);foreach(var path in (task.RepositoryEvidence??[]).Where(path=>evidencePaths is null||!evidencePaths.Contains(Normalize(path))).Take(5)){var safe=SafePath(path);Add("unsupported_repository_evidence",$"Task {task.Sequence} repository evidence '{safe}' is not present in the planning snapshot.",task.Sequence,safe);}}
  if(HasCycle(tasks))Add("dependency_cycle","Task dependency graph contains a cycle.");
  var text=string.Join(' ',tasks.Select(x=>$"{x.Title} {x.Description} {string.Join(' ',x.AcceptanceCriteria??[])}"));
  if(Placeholders.Any(x=>text.Contains(x,StringComparison.OrdinalIgnoreCase)))Add("placeholder","Placeholder wording is not allowed.");
  if(UnsupportedClaims.Any(x=>text.Contains(x,StringComparison.OrdinalIgnoreCase)))Add("false_execution_claim","Planner output cannot claim repository inspection or execution.");
  return Bound(e);
 }
 private static IReadOnlyList<PlannerValidationError> Bound(IEnumerable<PlannerValidationError> errors){const int maximumCount=10,maximumCharacters=2000;var result=new List<PlannerValidationError>();var used=0;foreach(var error in errors.Take(maximumCount)){var remaining=maximumCharacters-used;if(remaining<=0)break;var message=error.Message.Length<=remaining?error.Message:error.Message[..remaining];result.Add(error with{Message=message});used+=message.Length;}return result;}
 private static bool HasCycle(IReadOnlyList<PlannerTask> tasks){var map=tasks.ToDictionary(x=>x.Sequence);var visiting=new HashSet<int>();var visited=new HashSet<int>();bool Visit(int sequence){if(visited.Contains(sequence))return false;if(!visiting.Add(sequence))return true;foreach(var dependency in map[sequence].DependsOnSequences??[])if(map.ContainsKey(dependency)&&Visit(dependency))return true;visiting.Remove(sequence);visited.Add(sequence);return false;}return tasks.Any(x=>Visit(x.Sequence));}
 internal static string Normalize(string path){var value=(path??string.Empty).Trim().Replace('\\','/');while(value.StartsWith("./",StringComparison.Ordinal))value=value[2..];return value.TrimStart('/');}
 private static string SafePath(string path){var value=Normalize(path);if(Path.IsPathRooted(path)||value.Contains("../",StringComparison.Ordinal)||value=="..")return "invalid-relative-path";return value.Length<=240?value:value[..237]+"...";}
}

public static class PlannerEvidenceSanitizer
{
 public static PlannerEvidenceSanitization Sanitize(PlannerPlan plan,IReadOnlySet<string> allowed)
 {
  var canonical=allowed.ToDictionary(PlannerPlanValidator.Normalize,x=>x,StringComparer.OrdinalIgnoreCase);var unsupported=new List<PlannerValidationError>();var tasks=(plan.Tasks??[]).Select(task=>{var evidence=new List<string>();foreach(var raw in task.RepositoryEvidence??[]){var normalized=PlannerPlanValidator.Normalize(raw);if(canonical.TryGetValue(normalized,out var exact)){if(!evidence.Contains(exact,StringComparer.OrdinalIgnoreCase))evidence.Add(exact);}else{var safe=Path.IsPathRooted(raw)||normalized.Contains("../",StringComparison.Ordinal)||normalized==".."?"invalid-relative-path":normalized;safe=safe.Length<=240?safe:safe[..237]+"...";unsupported.Add(new("unsupported_repository_evidence",$"Task {task.Sequence} repository evidence '{safe}' is not present in the planning snapshot.",task.Sequence,safe));}}return task with{RepositoryEvidence=evidence};}).ToList();return new(plan with{Tasks=tasks},unsupported.Take(10).ToList());
 }
 public static bool OnlyEvidenceErrors(IReadOnlyList<PlannerValidationError> errors)=>errors.Count>0&&errors.All(x=>x.Code=="unsupported_repository_evidence");
 public static PlannerCorrectionContext BuildCorrection(IReadOnlyList<PlannerValidationError> errors,PlannerPlan previous,IReadOnlySet<string> allowed)
 {
  var suitable=allowed.Order(StringComparer.Ordinal).Take(40).ToList();var bounded=errors.Take(10).Select(x=>x.Code=="unsupported_repository_evidence"?x with{Message=$"{x.Message} Remove it or replace it with an exact allowed path; use [] when none apply."}:x).ToList();var prior=previous with{Summary=Limit(previous.Summary,1000),PlanningNotes=(previous.PlanningNotes??[]).Take(10).Select(x=>Limit(x,500)).ToList(),Tasks=(previous.Tasks??[]).Take(20).Select(x=>x with{Title=Limit(x.Title,200),Description=Limit(x.Description,1000),AcceptanceCriteria=(x.AcceptanceCriteria??[]).Take(10).Select(y=>Limit(y,500)).ToList()}).ToList()};return new(bounded,prior,suitable);
 }
 private static string Limit(string? value,int max)=>string.IsNullOrEmpty(value)?string.Empty:value.Length<=max?value:value[..max];
}

public static class RepositoryEvidencePathPolicy
{
 private static readonly string[] SensitiveNames=[".env",".git","id_rsa","id_ed25519","credentials","secrets.json"];
 public static bool IsSafe(string path){var normalized=PlannerPlanValidator.Normalize(path);return !Path.IsPathRooted(path)&&normalized!=".."&&!normalized.Contains("../",StringComparison.Ordinal)&&!string.IsNullOrWhiteSpace(normalized)&&!normalized.Split('/').Any(part=>SensitiveNames.Any(name=>part.Equals(name,StringComparison.OrdinalIgnoreCase)||part.StartsWith(name+".",StringComparison.OrdinalIgnoreCase)));}
 public static IReadOnlyList<string> Rank(IEnumerable<string> paths,string featureRequest,int maximum=500){var terms=featureRequest.Split([' ','-','_','/'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(x=>x.Length>=4).Distinct(StringComparer.OrdinalIgnoreCase).ToList();return paths.Select(PlannerPlanValidator.Normalize).Where(IsSafe).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path=>terms.Any(term=>path.Contains(term,StringComparison.OrdinalIgnoreCase))?0:IsManifest(path)?1:IsArchitecturePath(path)?2:3).ThenBy(path=>path,StringComparer.Ordinal).Take(maximum).ToList();}
 private static bool IsManifest(string path)=>new[]{".sln",".slnx",".csproj","package.json","vite.config","tsconfig","pom.xml","build.gradle","Cargo.toml","go.mod","requirements.txt","pyproject.toml"}.Any(name=>path.EndsWith(name,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(path).Equals(name,StringComparison.OrdinalIgnoreCase));
 private static bool IsArchitecturePath(string path)=>new[]{"domain","application","api","frontend","test","src"}.Any(part=>path.Split('/').Contains(part,StringComparer.OrdinalIgnoreCase));
}
