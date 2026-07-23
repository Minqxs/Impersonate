using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Impersonate.Infrastructure.Persistence.Configurations;

internal sealed class AiProviderConnectionConfiguration : IEntityTypeConfiguration<AiProviderConnection>
{
    public void Configure(EntityTypeBuilder<AiProviderConnection> b) { b.ToTable("AiProviderConnections"); b.HasKey(x => x.Id); b.Property(x => x.DisplayName).HasMaxLength(100); b.Property(x => x.LastFailureCode).HasMaxLength(100); b.Property(x => x.LastSafeFailureMessage).HasMaxLength(500); b.HasMany(x => x.Models).WithOne().HasForeignKey(x => x.ProviderConnectionId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class DiscoveredModelConfiguration : IEntityTypeConfiguration<DiscoveredModel>
{
    public void Configure(EntityTypeBuilder<DiscoveredModel> b) { b.ToTable("DiscoveredModels"); b.HasKey(x => x.Id); b.Property(x => x.ProviderModelId).HasMaxLength(300); b.Property(x => x.DisplayName).HasMaxLength(300); b.Property(x => x.Description).HasMaxLength(2000); b.Property(x => x.CapabilitiesJson).HasMaxLength(1000); b.HasIndex(x => new { x.ProviderConnectionId, x.ProviderModelId }).IsUnique(); }
}
internal sealed class ProjectAiRoutingPolicyConfiguration : IEntityTypeConfiguration<ProjectAiRoutingPolicy>
{
    public void Configure(EntityTypeBuilder<ProjectAiRoutingPolicy> b) { b.ToTable("ProjectAiRoutingPolicies"); b.HasKey(x => x.ProjectId); b.Property(x => x.AllowedProvidersJson).HasMaxLength(500); b.Property(x => x.BlockedProvidersJson).HasMaxLength(500); }
}
internal sealed class ModelSelectionDecisionConfiguration : IEntityTypeConfiguration<ModelSelectionDecision>
{
    public void Configure(EntityTypeBuilder<ModelSelectionDecision> b) { b.ToTable("ModelSelectionDecisions"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.Provider).HasMaxLength(50); b.Property(x => x.Model).HasMaxLength(300); b.Property(x => x.TaskProfileJson).HasMaxLength(12000); b.Property(x => x.Explanation).HasMaxLength(4000); b.Property(x => x.CandidateSummaryJson).HasMaxLength(16000); b.Property(x=>x.ScoreBreakdownJson).HasMaxLength(12000);b.Property(x=>x.MetadataVersion).HasMaxLength(100); b.HasIndex(x => new { x.ProjectId, x.PipelineRunId }); b.HasIndex(x => new { x.PlannedTaskId, x.TaskAttemptId, x.Role }); }
}
internal sealed class ProviderCredentialSecretConfiguration : IEntityTypeConfiguration<ProviderCredentialSecret>
{
    public void Configure(EntityTypeBuilder<ProviderCredentialSecret> b) { b.ToTable("ProviderCredentialSecrets"); b.HasKey(x => x.ConnectionId); b.Property(x => x.ProtectedPayload).HasMaxLength(8000); }
}
