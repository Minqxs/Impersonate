using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class ReviewDecisionConfiguration : IEntityTypeConfiguration<ReviewDecision>
{
    public void Configure(EntityTypeBuilder<ReviewDecision> b)
    {
        b.ToTable("ReviewDecisions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Decision).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Provider).HasMaxLength(50);
        b.Property(x => x.Model).HasMaxLength(300);
        b.Property(x => x.PromptVersion).HasMaxLength(50);
        b.Property(x => x.ProviderRequestId).HasMaxLength(200);
        b.Property(x => x.ReviewedPatchSha256).HasMaxLength(64);
        b.Property(x => x.FindingsJson).HasMaxLength(16000);
        b.Property(x => x.Summary).HasMaxLength(2000);
        b.Property(x => x.Feedback).HasMaxLength(4000);
        b.HasIndex(x => new { x.PlannedTaskId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.PlannedTaskId, x.IsCurrent });
        b.HasOne<TaskAttempt>().WithMany().HasForeignKey(x => x.TaskAttemptId).OnDelete(DeleteBehavior.NoAction);
    }
}
