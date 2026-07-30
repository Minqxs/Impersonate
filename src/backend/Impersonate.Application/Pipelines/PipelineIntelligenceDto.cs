using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record PipelineIntelligenceDto(Guid PipelineRunId, string? RepositoryContextSummary, IReadOnlyList<string> Languages, IReadOnlyList<string> Frameworks, IReadOnlyList<PlannedTaskDto> DependencyGraph, ExecutionReadinessDto Routing, string ActiveStage, Guid? ActiveTaskId, bool PreferReviewerDiversity, int ReviewerDiversityWeight, string HistoricalOutcomeMessage);
