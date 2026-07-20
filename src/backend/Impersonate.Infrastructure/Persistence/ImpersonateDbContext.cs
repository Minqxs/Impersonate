using Impersonate.Domain.Projects;
using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

public sealed class ImpersonateDbContext(DbContextOptions<ImpersonateDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<PlanningAttempt> PlanningAttempts => Set<PlanningAttempt>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ImpersonateDbContext).Assembly);
}
