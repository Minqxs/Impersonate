namespace Impersonate.Application.Quality;

public interface IProjectQualityCache
{
    bool TryGet(Guid projectId, out ProjectQualitySummary? summary);
    void Set(Guid projectId, ProjectQualitySummary summary, TimeSpan duration);
    void Remove(Guid projectId);
}
