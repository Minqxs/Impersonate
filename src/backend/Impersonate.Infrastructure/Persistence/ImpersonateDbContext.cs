using Impersonate.Domain.Projects;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.AiModels;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

public sealed class ImpersonateDbContext(DbContextOptions<ImpersonateDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<PlanningAttempt> PlanningAttempts => Set<PlanningAttempt>();
    public DbSet<AiModelProfile> AiModelProfiles => Set<AiModelProfile>();
    public DbSet<AgentModelAssignment> AgentModelAssignments => Set<AgentModelAssignment>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ImpersonateDbContext).Assembly);
}
