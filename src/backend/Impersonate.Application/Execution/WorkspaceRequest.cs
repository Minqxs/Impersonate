using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record WorkspaceRequest(Guid ProjectId, Guid PipelineRunId, Guid PlannedTaskId, int AttemptNumber, string RepositoryUrl, string DefaultBranch, IReadOnlyList<WorkspacePatchReference> ApprovedDependencyPatches, string? CurrentPatchReference);
