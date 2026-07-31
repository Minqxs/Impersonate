using Impersonate.Domain.Projects;
using Impersonate.Domain.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class ProjectCodeQualityConfigurationConfiguration : IEntityTypeConfiguration<ProjectCodeQualityConfiguration>
{
    public void Configure(EntityTypeBuilder<ProjectCodeQualityConfiguration> b)
    {
        b.ToTable("ProjectCodeQualityConfigurations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.ProjectId).IsUnique();
        b.Property(x => x.BaseUrl).HasMaxLength(500);
        b.Property(x => x.ProjectKey).HasMaxLength(400);
        b.Property(x => x.DisplayName).HasMaxLength(200);
        b.Property(x => x.LastFailureCode).HasMaxLength(100);
        b.Property(x => x.LastSafeFailureMessage).HasMaxLength(500);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
