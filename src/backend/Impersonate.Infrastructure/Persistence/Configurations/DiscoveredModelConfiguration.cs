using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class DiscoveredModelConfiguration : IEntityTypeConfiguration<DiscoveredModel>
{
    public void Configure(EntityTypeBuilder<DiscoveredModel> b)
    {
        b.ToTable("DiscoveredModels");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProviderModelId).HasMaxLength(300);
        b.Property(x => x.DisplayName).HasMaxLength(300);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CapabilitiesJson).HasMaxLength(1000);
        b.HasIndex(x => new { x.ProviderConnectionId, x.ProviderModelId }).IsUnique();
    }
}
