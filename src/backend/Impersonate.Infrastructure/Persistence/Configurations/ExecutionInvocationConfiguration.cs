using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class ExecutionInvocationConfiguration : IEntityTypeConfiguration<ExecutionInvocation>
{
    public void Configure(EntityTypeBuilder<ExecutionInvocation> b)
    {
        b.ToTable("ExecutionInvocations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.AgentRole).HasMaxLength(20);
        b.Property(x => x.Provider).HasMaxLength(50);
        b.Property(x => x.Model).HasMaxLength(300);
        b.Property(x => x.PromptVersion).HasMaxLength(50);
        b.Property(x => x.ProviderRequestId).HasMaxLength(200);
        b.Property(x => x.ResponseType).HasMaxLength(40);
        b.Property(x => x.ProviderResponseStatus).HasMaxLength(40);
        b.Property(x => x.ProviderIncompleteReason).HasMaxLength(100);
        b.Property(x => x.CurrentPhase).HasMaxLength(30);
        b.Property(x => x.RequestedProhibitedTool).HasMaxLength(50);
        b.Property(x => x.LastPatchFailureCode).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FailureCode).HasMaxLength(100);
        b.Property(x => x.FailureReason).HasMaxLength(2000);
        b.HasIndex(x => new { x.TaskAttemptId, x.Sequence }).IsUnique();
        b.HasOne<TaskAttempt>().WithMany().HasForeignKey(x => x.TaskAttemptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Impersonate.Domain.Ai.ModelSelectionDecision>().WithMany().HasForeignKey(x => x.SelectionDecisionId).OnDelete(DeleteBehavior.NoAction);
    }
}
