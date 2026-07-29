using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class AiProviderConnectionConfiguration : IEntityTypeConfiguration<AiProviderConnection>
{
    public void Configure(EntityTypeBuilder<AiProviderConnection> b)
    {
        b.ToTable("AiProviderConnections");
        b.HasKey(x => x.Id);
        b.Property(x => x.DisplayName).HasMaxLength(100);
        b.Property(x => x.LastFailureCode).HasMaxLength(100);
        b.Property(x => x.LastSafeFailureMessage).HasMaxLength(500);
        b.HasMany(x => x.Models).WithOne().HasForeignKey(x => x.ProviderConnectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
