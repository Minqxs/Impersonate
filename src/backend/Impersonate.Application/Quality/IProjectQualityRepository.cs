using Impersonate.Domain.Quality;

namespace Impersonate.Application.Quality;

public interface IProjectQualityRepository
{
    Task<ProjectCodeQualityConfiguration?> GetAsync(Guid projectId, CancellationToken cancellationToken);
    Task AddAsync(ProjectCodeQualityConfiguration configuration, CancellationToken cancellationToken);
    void Remove(ProjectCodeQualityConfiguration configuration);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
