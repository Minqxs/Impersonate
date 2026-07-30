using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record CoderContext(Guid ProjectId, Guid PipelineRunId, string FeatureRequest, Guid PlannedTaskId, string TaskTitle, string TaskDescription, IReadOnlyList<string> AcceptanceCriteria, int AttemptNumber, int RevisionNumber, string? ReviewerFeedback, IReadOnlyList<string> EarlierApprovedSummaries, WorkspaceReference Workspace, SelectedModel Model, string PromptVersion = "coder-v1", IReadOnlyList<string>? RepositoryEvidence = null, string? PriorProtocolSummary = null, int? ExpectedDiffTokens = null);
