using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
namespace Impersonate.Application.Execution;

public sealed record WorkspaceRequest(Guid ProjectId,Guid PipelineRunId,Guid PlannedTaskId,int AttemptNumber,string RepositoryUrl,string DefaultBranch,IReadOnlyList<string> ApprovedPatchReferences,string? CurrentPatchReference);
public sealed record WorkspaceReference(string Value);
public sealed record WorkspacePreparationResult(bool Succeeded,WorkspaceReference? Workspace,string? FailureCode,string? FailureMessage);
public interface IRepositoryWorkspaceService { Task<WorkspacePreparationResult> PrepareAsync(WorkspaceRequest request,CancellationToken ct); Task CleanupAsync(WorkspaceReference workspace,CancellationToken ct); }
public interface IChildProcessEnvironmentBuilder { IReadOnlyDictionary<string,string> Build(); }
public sealed record ExecutionEnvironmentReadiness(bool Ready,string OperatingSystem,bool GitAvailable,bool GitVersionSucceeded,bool WorkspaceRootWritable,bool CoreEnvironmentValid,bool SanitizedProcessSucceeded,IReadOnlyList<string> SuppliedVariableNames,IReadOnlyList<string> Blockers);
public interface IExecutionEnvironmentReadinessService { Task<ExecutionEnvironmentReadiness> CheckAsync(CancellationToken ct); }
public sealed record ArtifactScope(Guid ProjectId,Guid PipelineRunId,Guid PlannedTaskId,int AttemptNumber);
public sealed record StoredArtifact(string Reference,string Sha256,long ContentLength,string MediaType,DateTimeOffset CreatedAtUtc);
public interface IExecutionArtifactStore { Task<StoredArtifact> WriteTextAsync(ArtifactScope scope,string name,string content,string mediaType,CancellationToken ct); Task<string> ReadTextAsync(string reference,int maximumCharacters,CancellationToken ct); }
public sealed record RepositoryToolResult(bool Succeeded,string Output,string? FailureCode=null,string? FailureMessage=null,bool Truncated=false);
public sealed record RepositoryCommand(string Executable,IReadOnlyList<string> Arguments,string? WorkingDirectory=null,int TimeoutSeconds=120);
public interface IRepositoryTools
{
 Task<RepositoryToolResult> ListFilesAsync(WorkspaceReference workspace,string relativePath,CancellationToken ct); Task<RepositoryToolResult> ReadFileAsync(WorkspaceReference workspace,string relativePath,CancellationToken ct); Task<RepositoryToolResult> SearchTextAsync(WorkspaceReference workspace,string query,string relativePath,CancellationToken ct); Task<RepositoryToolResult> ApplyPatchAsync(WorkspaceReference workspace,string patch,CancellationToken ct); Task<RepositoryToolResult> GetDiffAsync(WorkspaceReference workspace,CancellationToken ct); Task<RepositoryToolResult> RunCommandAsync(WorkspaceReference workspace,RepositoryCommand command,CancellationToken ct);
}
public sealed record CoderContext(Guid ProjectId,Guid PipelineRunId,string FeatureRequest,Guid PlannedTaskId,string TaskTitle,string TaskDescription,IReadOnlyList<string> AcceptanceCriteria,int AttemptNumber,int RevisionNumber,string? ReviewerFeedback,IReadOnlyList<string> EarlierApprovedSummaries,WorkspaceReference Workspace,SelectedModel Model,string PromptVersion="coder-v1");
public sealed record CoderResult(bool Succeeded,string Summary,IReadOnlyList<string> ChangedFiles,IReadOnlyList<string> ValidationNotes,int ToolStepCount,string? ProviderRequestId,int? InputTokenCount,int? OutputTokenCount,string? FailureCode=null,string? FailureMessage=null);
public interface ICoderAgent { Task<CoderResult> ExecuteAsync(CoderContext context,CancellationToken ct); }
public sealed record ReviewFinding(string Severity,string Message,string? Path=null,int? Line=null);
public sealed record ReviewerContext(Guid ProjectId,Guid PipelineRunId,string FeatureRequest,Guid PlannedTaskId,string TaskTitle,string TaskDescription,IReadOnlyList<string> AcceptanceCriteria,int AttemptNumber,string Patch,string PatchSha256,IReadOnlyList<string> ChangedFiles,IReadOnlyList<string> ValidationResults,string CoderSummary,string? PriorFeedback,WorkspaceReference Workspace,SelectedModel Model,string PromptVersion="reviewer-v1");
public sealed record ReviewerResult(bool Succeeded,ReviewDecisionType? Decision,string Summary,string? Feedback,IReadOnlyList<ReviewFinding> Findings,string? ProviderRequestId,int? InputTokenCount,int? OutputTokenCount,string? FailureCode=null,string? FailureMessage=null);
public interface IReviewerAgent { Task<ReviewerResult> ReviewAsync(ReviewerContext context,CancellationToken ct); }
public sealed class ExecutionOptions { public string? WorkspaceRoot{get;set;} public string? ArtifactRoot{get;set;} public int MaximumArtifactBytes{get;set;}=2_000_000; public int MaximumToolOutputCharacters{get;set;}=100_000; public int MaximumCoderSteps{get;set;}=20; public int MaximumModelFallbacks{get;set;}=2; public int MaximumModelInputTokens{get;set;}=24_000; public int CommandTimeoutSeconds{get;set;}=120; public int ClaimMinutes{get;set;}=15; public int MaximumWorkspacePreparationAttempts{get;set;}=3; }
