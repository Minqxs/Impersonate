using Impersonate.Domain.Delivery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class RunDeliveryReviewConfiguration : IEntityTypeConfiguration<RunDeliveryReview>
{
    public void Configure(EntityTypeBuilder<RunDeliveryReview> b)
    {
        b.ToTable("RunDeliveryReviews");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Provider).HasMaxLength(50);
        b.Property(x => x.Model).HasMaxLength(200);
        b.Property(x => x.ExactHeadSha).HasMaxLength(64);
        b.Property(x => x.Summary).HasMaxLength(2000);
        b.Property(x => x.Feedback).HasMaxLength(4000);
        b.Property(x => x.FindingsJson).HasMaxLength(16000);
        b.Ignore(x => x.IsCurrent);
        b.HasIndex(x => new { x.RunDeliveryId, x.AttemptNumber }).IsUnique();
        b.HasIndex(x => new { x.RunDeliveryId, x.ExactHeadSha });
        b.HasOne<RunDelivery>().WithMany().HasForeignKey(x => x.RunDeliveryId).OnDelete(DeleteBehavior.Cascade);
    }
}
