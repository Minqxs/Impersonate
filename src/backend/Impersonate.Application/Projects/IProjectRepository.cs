using Impersonate.Domain.Projects;

namespace Impersonate.Application.Projects;

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, string? search, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
