using Impersonate.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

public sealed class ImpersonateDbContext(DbContextOptions<ImpersonateDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ImpersonateDbContext).Assembly);
}
