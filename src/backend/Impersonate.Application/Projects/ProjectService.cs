using Impersonate.Domain.Projects;

namespace Impersonate.Application.Projects;

internal sealed class ProjectService(IProjectRepository repository) : IProjectService
{
    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken) { var project = Project.Create(request.Name, request.Description, request.RepositoryUrl, request.DefaultBranch, request.Status); await repository.AddAsync(project, cancellationToken); await repository.SaveChangesAsync(cancellationToken); return Map(project); }
    public async Task<ProjectDto?> GetAsync(Guid projectId, CancellationToken cancellationToken) => (await repository.GetAsync(projectId, cancellationToken)) is { } p ? Map(p) : null;
    public async Task<IReadOnlyList<ProjectDto>> ListAsync(ProjectStatus? status, string? search, CancellationToken cancellationToken) => (await repository.ListAsync(status, search, cancellationToken)).Select(Map).ToList();
    public async Task<ProjectDto?> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken) { var p = await repository.GetAsync(projectId, cancellationToken); if (p is null) return null; p.UpdateDetails(request.Name, request.Description, request.RepositoryUrl, request.DefaultBranch); await repository.SaveChangesAsync(cancellationToken); return Map(p); }
    public async Task<ProjectDto?> ChangeStatusAsync(Guid projectId, ProjectStatus status, CancellationToken cancellationToken) { var p = await repository.GetAsync(projectId, cancellationToken); if (p is null) return null; p.ChangeStatus(status); await repository.SaveChangesAsync(cancellationToken); return Map(p); }
    public async Task<ProjectHealthSummaryDto?> GetHealthAsync(Guid projectId, CancellationToken cancellationToken) { var p = await repository.GetAsync(projectId, cancellationToken); if (p is null) return null; return new(projectId, "Unknown", [new("RepositoryConfigured", "Healthy", "A repository URL is configured."), new("DefaultBranchConfigured", "Healthy", "A default branch is configured.")], DateTimeOffset.UtcNow); }
    private static ProjectDto Map(Project p) => new(p.Id, p.Name, p.Description, p.RepositoryUrl, p.DefaultBranch, p.Status, p.CreatedAtUtc, p.UpdatedAtUtc);
}
