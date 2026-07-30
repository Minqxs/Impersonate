using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class ModelSelectionDecisionConfiguration : IEntityTypeConfiguration<ModelSelectionDecision>
{
    public void Configure(EntityTypeBuilder<ModelSelectionDecision> b)
    {
        b.ToTable("ModelSelectionDecisions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Provider).HasMaxLength(50);
        b.Property(x => x.Model).HasMaxLength(300);
        b.Property(x => x.TaskProfileJson).HasMaxLength(12000);
        b.Property(x => x.Explanation).HasMaxLength(4000);
        b.Property(x => x.CandidateSummaryJson).HasMaxLength(16000);
        b.Property(x => x.ScoreBreakdownJson).HasMaxLength(12000);
        b.Property(x => x.MetadataVersion).HasMaxLength(100);
        b.HasIndex(x => new { x.ProjectId, x.PipelineRunId });
        b.HasIndex(x => new { x.PlannedTaskId, x.TaskAttemptId, x.Role });
    }
}
