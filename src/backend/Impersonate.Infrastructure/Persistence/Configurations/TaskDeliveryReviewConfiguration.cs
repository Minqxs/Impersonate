using Impersonate.Domain.Delivery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class TaskDeliveryReviewConfiguration : IEntityTypeConfiguration<TaskDeliveryReview>
{
    public void Configure(EntityTypeBuilder<TaskDeliveryReview> b)
    {
        b.ToTable("TaskDeliveryReviews");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Provider).HasMaxLength(50);
        b.Property(x => x.Model).HasMaxLength(200);
        b.Property(x => x.ExactHeadSha).HasMaxLength(64);
        b.Property(x => x.Summary).HasMaxLength(2000);
        b.Property(x => x.Feedback).HasMaxLength(4000);
        b.Property(x => x.FindingsJson).HasMaxLength(16000);
        b.Ignore(x => x.IsCurrent);
        b.HasIndex(x => new { x.TaskDeliveryId, x.ReviewAttemptNumber }).IsUnique();
        b.HasIndex(x => new { x.TaskDeliveryId, x.ExactHeadSha });
        b.HasOne<TaskDelivery>().WithMany().HasForeignKey(x => x.TaskDeliveryId).OnDelete(DeleteBehavior.Cascade);
    }
}
