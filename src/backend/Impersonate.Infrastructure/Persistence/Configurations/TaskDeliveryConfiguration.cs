using Impersonate.Domain.Delivery;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class TaskDeliveryConfiguration : IEntityTypeConfiguration<TaskDelivery>
{
    public void Configure(EntityTypeBuilder<TaskDelivery> b)
    {
        b.ToTable("TaskDeliveries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.SourceBaseCommitSha).HasMaxLength(64);
        b.Property(x => x.DeliveryBaseCommitSha).HasMaxLength(64);
        b.Property(x => x.ApprovedPatchArtifactReference).HasMaxLength(500);
        b.Property(x => x.ApprovedPatchSha256).HasMaxLength(64);
        b.Property(x => x.IdempotencyKey).HasMaxLength(64);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.BranchName).HasMaxLength(250);
        b.Property(x => x.CommitSha).HasMaxLength(64);
        b.Property(x => x.ValidationSummaryJson).HasMaxLength(16000).HasDefaultValue("[]");
        b.Property(x => x.ClaimOwner).HasMaxLength(200);
        b.Property(x => x.PullRequestProvider).HasMaxLength(50);
        b.Property(x => x.PullRequestRepository).HasMaxLength(300);
        b.Property(x => x.PullRequestUrl).HasMaxLength(1000);
        b.Property(x => x.FailureCode).HasMaxLength(100);
        b.Property(x => x.FailureMessage).HasMaxLength(1000);
        b.Ignore(x => x.IsActive);
        b.HasIndex(x => x.PlannedTaskId).IsUnique();
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasIndex(x => new { x.PipelineRunId, x.Status });
        b.HasIndex(x => new { x.ProjectId, x.Status });
        b.HasIndex(x => new { x.Status, x.ClaimExpiresAtUtc, x.TaskSequence });
        b.HasIndex(x => new { x.PullRequestRepository, x.PullRequestNumber }).IsUnique().HasFilter("[PullRequestRepository] IS NOT NULL AND [PullRequestNumber] IS NOT NULL");
        b.HasIndex(x => new { x.ProjectId, x.BranchName }).IsUnique().HasFilter("[BranchName] IS NOT NULL");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<PipelineRun>().WithMany(x => x.Deliveries).HasForeignKey(x => x.PipelineRunId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<PlannedTask>().WithOne().HasForeignKey<TaskDelivery>(x => x.PlannedTaskId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ReviewDecision>().WithMany().HasForeignKey(x => x.ApprovedReviewDecisionId).OnDelete(DeleteBehavior.NoAction);
    }
}
