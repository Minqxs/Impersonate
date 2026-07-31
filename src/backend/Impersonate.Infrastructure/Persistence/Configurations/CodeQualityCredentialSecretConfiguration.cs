using Impersonate.Infrastructure.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class CodeQualityCredentialSecretConfiguration : IEntityTypeConfiguration<CodeQualityCredentialSecret>
{
    public void Configure(EntityTypeBuilder<CodeQualityCredentialSecret> b)
    {
        b.ToTable("CodeQualityCredentialSecrets");
        b.HasKey(x => x.ConfigurationId);
        b.Property(x => x.ProtectedPayload).HasMaxLength(8000);
    }
}
