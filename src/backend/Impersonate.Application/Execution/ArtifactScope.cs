using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record ArtifactScope(Guid ProjectId, Guid PipelineRunId, Guid PlannedTaskId, int AttemptNumber);
