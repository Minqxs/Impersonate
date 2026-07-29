using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class PipelineRunEventConfiguration : IEntityTypeConfiguration<PipelineRunEvent>
{
    public void Configure(EntityTypeBuilder<PipelineRunEvent> b)
    {
        b.ToTable("PipelineRunEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.EventType).HasMaxLength(100);
        b.Property(x => x.PreviousState).HasMaxLength(50);
        b.Property(x => x.NewState).HasMaxLength(50);
        b.Property(x => x.Message).HasMaxLength(2000);
        b.HasIndex(x => new { x.PipelineRunId, x.Sequence }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
    }
}
