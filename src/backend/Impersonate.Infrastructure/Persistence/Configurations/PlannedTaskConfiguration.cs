using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class PlannedTaskConfiguration : IEntityTypeConfiguration<PlannedTask>
{
    public void Configure(EntityTypeBuilder<PlannedTask> b)
    {
        b.ToTable("PlannedTasks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.AcceptanceCriteriaJson).HasMaxLength(8000);
        b.Ignore(x => x.AcceptanceCriteria);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.DependsOnTaskIdsJson).HasMaxLength(8000);
        b.Property(x => x.AffectedAreasJson).HasMaxLength(8000);
        b.Property(x => x.ChangeType).HasMaxLength(100);
        b.Property(x => x.Risk).HasMaxLength(30);
        b.Property(x => x.ConflictRisk).HasMaxLength(30);
        b.Property(x => x.ExecutionReason).HasMaxLength(1000);
        b.Property(x => x.RepositoryEvidenceJson).HasMaxLength(16000);
        b.Property(x => x.OrderAdjustmentReason).HasMaxLength(1000);
        b.HasIndex(x => new { x.PipelineRunId, x.Sequence }).IsUnique();
        b.HasMany(x => x.Attempts).WithOne().HasForeignKey(x => x.PlannedTaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.ReviewDecisions).WithOne().HasForeignKey(x => x.PlannedTaskId).OnDelete(DeleteBehavior.Restrict);
    }
}
