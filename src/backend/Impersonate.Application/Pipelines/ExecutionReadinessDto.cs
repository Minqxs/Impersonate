using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record ExecutionReadinessDto(bool Ready, ModelPreviewDto Coder, ModelPreviewDto Reviewer, IReadOnlyList<string> Blockers, IReadOnlyList<TaskRoutingPreviewDto>? Tasks = null, int DistinctCoderModels = 0, int DistinctReviewerModels = 0, int TasksUsingOverrides = 0);
