using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public interface IPlanningRepositoryContextService
{
    Task<PlanningRepositoryContextResult> BuildAsync(Guid projectId, Guid pipelineRunId, string repositoryUrl, string defaultBranch, string featureRequest, CancellationToken cancellationToken);
}
