using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class PipelineRunConfiguration : IEntityTypeConfiguration<PipelineRun>
{
    public void Configure(EntityTypeBuilder<PipelineRun> b)
    {
        b.ToTable("PipelineRuns");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.FeatureRequest).HasMaxLength(PipelineRun.FeatureRequestMaxLength).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.ExecutionWorkerId).HasMaxLength(200);
        b.Property(x => x.PlanningContextArtifactReference).HasMaxLength(500);
        b.Property(x => x.PlanningContextSummary).HasMaxLength(2000);
        b.Property(x => x.PlanningLanguagesJson).HasMaxLength(4000);
        b.Property(x => x.PlanningFrameworksJson).HasMaxLength(4000);
        b.Property(x => x.PlanningWarningsJson).HasMaxLength(4000);
        b.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.ProjectId, x.Status });
        b.HasIndex(x => new { x.Status, x.ExecutionClaimExpiresAtUtc });
        b.HasOne<Impersonate.Domain.Projects.Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LoopRun).WithOne().HasForeignKey<LoopRun>(x => x.PipelineRunId).OnDelete(DeleteBehavior.Restrict);
    }
}
