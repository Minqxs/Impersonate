using Impersonate.Domain.Projects;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

public sealed class ImpersonateDbContext(DbContextOptions<ImpersonateDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<PlanningAttempt> PlanningAttempts => Set<PlanningAttempt>();
    public DbSet<TaskAttempt> TaskAttempts => Set<TaskAttempt>();
    public DbSet<ReviewDecision> ReviewDecisions => Set<ReviewDecision>();
    public DbSet<AiProviderConnection> AiProviderConnections => Set<AiProviderConnection>();
    public DbSet<DiscoveredModel> DiscoveredModels => Set<DiscoveredModel>();
    public DbSet<ProjectAiRoutingPolicy> ProjectAiRoutingPolicies => Set<ProjectAiRoutingPolicy>();
    public DbSet<ModelSelectionDecision> ModelSelectionDecisions => Set<ModelSelectionDecision>();
    public DbSet<ProviderCredentialSecret> ProviderCredentialSecrets => Set<ProviderCredentialSecret>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ImpersonateDbContext).Assembly);
}
