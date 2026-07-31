using Impersonate.Application.Quality;
using Impersonate.Domain.Quality;
using Microsoft.EntityFrameworkCore;
namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfProjectQualityRepository(ImpersonateDbContext db) : IProjectQualityRepository
{
    public Task<ProjectCodeQualityConfiguration?> GetAsync(Guid projectId, CancellationToken ct) => db.ProjectCodeQualityConfigurations.SingleOrDefaultAsync(x => x.ProjectId == projectId, ct);
    public Task AddAsync(ProjectCodeQualityConfiguration configuration, CancellationToken ct) => db.ProjectCodeQualityConfigurations.AddAsync(configuration, ct).AsTask();
    public void Remove(ProjectCodeQualityConfiguration configuration) => db.ProjectCodeQualityConfigurations.Remove(configuration);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
