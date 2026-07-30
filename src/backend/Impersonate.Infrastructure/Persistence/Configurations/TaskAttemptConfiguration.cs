using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class TaskAttemptConfiguration : IEntityTypeConfiguration<TaskAttempt>
{
    public void Configure(EntityTypeBuilder<TaskAttempt> b)
    {
        b.ToTable("TaskAttempts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.AttemptType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Provider).HasMaxLength(50);
        b.Property(x => x.Model).HasMaxLength(300);
        b.Property(x => x.PromptVersion).HasMaxLength(50);
        b.Property(x => x.ProviderRequestId).HasMaxLength(200);
        b.Property(x => x.Summary).HasMaxLength(2000);
        b.Property(x => x.FailureCode).HasMaxLength(100);
        b.Property(x => x.FailureReason).HasMaxLength(2000);
        b.Property(x => x.ChangedFilesJson).HasMaxLength(16000);
        b.Property(x => x.PatchArtifactReference).HasMaxLength(500);
        b.Property(x => x.PatchSha256).HasMaxLength(64);
        b.Property(x => x.ValidationSummaryJson).HasMaxLength(16000);
        b.Property(x => x.SourceBaseCommitSha).HasMaxLength(64);
        b.Property(x => x.DependencyTaskIdsJson).HasMaxLength(16000);
        b.Property(x => x.ComposedTreeFingerprint).HasMaxLength(64);
        b.Property(x => x.CompositionStatus).HasMaxLength(30);
        b.HasIndex(x => new { x.PlannedTaskId, x.AttemptNumber }).IsUnique();
        b.HasIndex(x => x.Status);
    }
}
