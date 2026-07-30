using Impersonate.Application.Projects;
using Impersonate.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfProjectRepository(ImpersonateDbContext dbContext) : IProjectRepository
{
    public Task AddAsync(Project project, CancellationToken cancellationToken) => dbContext.Projects.AddAsync(project, cancellationToken).AsTask();
    public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) => dbContext.Projects.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    public async Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.Projects.AsNoTracking().AsQueryable();
        if (status is not null)
            query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Name.Contains(term));
        }
        return await query.OrderBy(p => p.Status).ThenBy(p => p.Name).ToListAsync(cancellationToken);
    }
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
