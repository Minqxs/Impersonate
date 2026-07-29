using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record ReviewerContext(Guid ProjectId, Guid PipelineRunId, string FeatureRequest, Guid PlannedTaskId, string TaskTitle, string TaskDescription, IReadOnlyList<string> AcceptanceCriteria, int AttemptNumber, string Patch, string PatchSha256, IReadOnlyList<string> ChangedFiles, IReadOnlyList<string> ValidationResults, string CoderSummary, string? PriorFeedback, WorkspaceReference Workspace, SelectedModel Model, string PromptVersion = "reviewer-v1");
