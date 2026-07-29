using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class ProviderCredentialSecretConfiguration : IEntityTypeConfiguration<ProviderCredentialSecret>
{
    public void Configure(EntityTypeBuilder<ProviderCredentialSecret> b)
    {
        b.ToTable("ProviderCredentialSecrets");
        b.HasKey(x => x.ConnectionId);
        b.Property(x => x.ProtectedPayload).HasMaxLength(8000);
    }
}
