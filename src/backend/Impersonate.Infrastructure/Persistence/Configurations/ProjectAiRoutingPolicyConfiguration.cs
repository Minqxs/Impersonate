using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class ProjectAiRoutingPolicyConfiguration : IEntityTypeConfiguration<ProjectAiRoutingPolicy>
{
    public void Configure(EntityTypeBuilder<ProjectAiRoutingPolicy> b)
    {
        b.ToTable("ProjectAiRoutingPolicies");
        b.HasKey(x => x.ProjectId);
        b.Property(x => x.AllowedProvidersJson).HasMaxLength(500);
        b.Property(x => x.BlockedProvidersJson).HasMaxLength(500);
    }
}
