using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public interface IProjectAiService
{
    Task<ProjectAiReadiness?> GetReadinessAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ModelSelectionResult?> PreviewAsync(Guid projectId, AgentRole role, string description, Guid? manualModelOverrideId, CancellationToken cancellationToken);
}
