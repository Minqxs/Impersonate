namespace Impersonate.Application.Quality;

public interface IProjectQualityService
{
    Task<ProjectQualityConfigurationDto?> GetConfigurationAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ProjectQualityConfigurationDto?> SaveAsync(Guid projectId, SaveProjectQualityConfigurationRequest request, CancellationToken cancellationToken);
    Task<ProjectQualitySummary?> GetSummaryAsync(Guid projectId, bool forceRefresh, CancellationToken cancellationToken);
    Task<ProjectQualitySummary?> ValidateAsync(Guid projectId, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(Guid projectId, CancellationToken cancellationToken);
}
