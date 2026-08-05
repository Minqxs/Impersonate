using Impersonate.Domain.Delivery;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class RunDeliveryConfiguration : IEntityTypeConfiguration<RunDelivery>
{
    public void Configure(EntityTypeBuilder<RunDelivery> b)
    {
        b.ToTable("RunDeliveries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.SourceDefaultBranch).HasMaxLength(200);
        b.Property(x => x.SourceBaseCommitSha).HasMaxLength(64);
        b.Property(x => x.RunBranchName).HasMaxLength(250);
        b.Property(x => x.RunBranchHeadSha).HasMaxLength(64);
        b.Property(x => x.AggregateValidationSummaryJson).HasMaxLength(16000);
        b.Property(x => x.FinalReviewedHeadSha).HasMaxLength(64);
        b.Property(x => x.FinalPullRequestProvider).HasMaxLength(50);
        b.Property(x => x.FinalPullRequestRepository).HasMaxLength(300);
        b.Property(x => x.FinalPullRequestUrl).HasMaxLength(1000);
        b.Property(x => x.FinalPullRequestHeadSha).HasMaxLength(64);
        b.Property(x => x.FinalPullRequestBaseBranch).HasMaxLength(200);
        b.Property(x => x.FinalPullRequestMergeableState).HasMaxLength(50);
        b.Property(x => x.RequiredChecksState).HasMaxLength(50);
        b.Property(x => x.FailureCode).HasMaxLength(100);
        b.Property(x => x.FailureMessage).HasMaxLength(1000);
        b.Property(x => x.ClaimOwner).HasMaxLength(200);
        b.HasIndex(x => x.PipelineRunId).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.Status });
        b.HasIndex(x => new { x.ProjectId, x.RunBranchName }).IsUnique();
        b.HasIndex(x => new { x.FinalPullRequestRepository, x.FinalPullRequestNumber }).IsUnique().HasFilter("[FinalPullRequestNumber] IS NOT NULL");
        b.HasIndex(x => new { x.Status, x.ClaimExpiresAtUtc, x.UpdatedAtUtc, x.Id });
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<PipelineRun>().WithOne(x => x.RunDelivery).HasForeignKey<RunDelivery>(x => x.PipelineRunId).OnDelete(DeleteBehavior.Cascade);
    }
}
