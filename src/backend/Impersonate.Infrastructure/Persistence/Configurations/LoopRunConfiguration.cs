using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class LoopRunConfiguration : IEntityTypeConfiguration<LoopRun>
{
    public void Configure(EntityTypeBuilder<LoopRun> b)
    {
        b.ToTable("LoopRuns");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.LoopDefinitionId).HasMaxLength(100);
        b.Property(x => x.LoopDefinitionVersion).HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.CurrentStage).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.PipelineRunId).IsUnique();
    }
}
