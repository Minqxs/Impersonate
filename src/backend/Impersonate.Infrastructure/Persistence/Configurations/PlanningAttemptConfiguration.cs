using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class PlanningAttemptConfiguration : IEntityTypeConfiguration<PlanningAttempt>
{
    public void Configure(EntityTypeBuilder<PlanningAttempt> b)
    {
        b.ToTable("PlanningAttempts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(50);
        b.Property(x => x.Model).HasMaxLength(200);
        b.Property(x => x.PromptVersion).HasMaxLength(50);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.FailureCode).HasMaxLength(100);
        b.Property(x => x.FailureMessage).HasMaxLength(2000);
        b.Property(x => x.ProviderRequestId).HasMaxLength(200);
        b.HasIndex(x => new { x.PipelineRunId, x.AttemptNumber }).IsUnique();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.StartedAtUtc);
        b.HasOne<PipelineRun>().WithMany().HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.Restrict);
    }
}
