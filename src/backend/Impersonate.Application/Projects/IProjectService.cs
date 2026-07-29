using Impersonate.Domain.Projects;

namespace Impersonate.Application.Projects;

public interface IProjectService
{
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectDto?> GetAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectDto>> ListAsync(ProjectStatus? status, string? search, CancellationToken cancellationToken);
    Task<ProjectDto?> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectDto?> ChangeStatusAsync(Guid projectId, ProjectStatus status, CancellationToken cancellationToken);
    Task<ProjectHealthSummaryDto?> GetHealthAsync(Guid projectId, CancellationToken cancellationToken);
}
