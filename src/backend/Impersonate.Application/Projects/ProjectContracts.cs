using Impersonate.Domain.Projects;

namespace Impersonate.Application.Projects;

public sealed record ProjectDto(Guid Id, string Name, string? Description, string RepositoryUrl, string DefaultBranch, ProjectStatus Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record CreateProjectRequest(string Name, string? Description, string RepositoryUrl, string DefaultBranch, ProjectStatus Status = ProjectStatus.Idle);
public sealed record UpdateProjectRequest(string Name, string? Description, string RepositoryUrl, string DefaultBranch);
public sealed record ProjectHealthCheckDto(string Name, string Status, string Message);
public sealed record ProjectHealthSummaryDto(Guid ProjectId, string OverallStatus, IReadOnlyList<ProjectHealthCheckDto> Checks, DateTimeOffset CheckedAtUtc);

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, string? search, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IProjectService
{
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectDto?> GetAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectDto>> ListAsync(ProjectStatus? status, string? search, CancellationToken cancellationToken);
    Task<ProjectDto?> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectDto?> ChangeStatusAsync(Guid projectId, ProjectStatus status, CancellationToken cancellationToken);
    Task<ProjectHealthSummaryDto?> GetHealthAsync(Guid projectId, CancellationToken cancellationToken);
}
