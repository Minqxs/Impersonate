using Impersonate.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(Project.NameMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(Project.DescriptionMaxLength);
        builder.Property(x => x.RepositoryUrl).HasMaxLength(Project.RepositoryUrlMaxLength).IsRequired();
        builder.Property(x => x.DefaultBranch).HasMaxLength(Project.DefaultBranchMaxLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.Status);
    }
}
