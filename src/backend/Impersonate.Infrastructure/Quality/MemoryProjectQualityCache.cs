using Impersonate.Application.Quality;
using Microsoft.Extensions.Caching.Memory;
namespace Impersonate.Infrastructure.Quality;

internal sealed class MemoryProjectQualityCache(IMemoryCache cache) : IProjectQualityCache
{
    private static string Key(Guid id) => $"project-quality:{id}";
    public bool TryGet(Guid id, out ProjectQualitySummary? summary) => cache.TryGetValue(Key(id), out summary);
    public void Set(Guid id, ProjectQualitySummary summary, TimeSpan duration) => cache.Set(Key(id), summary, duration);
    public void Remove(Guid id) => cache.Remove(Key(id));
}
