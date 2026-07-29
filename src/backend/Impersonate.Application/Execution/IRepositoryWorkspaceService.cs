using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public interface IRepositoryWorkspaceService
{
    Task<WorkspacePreparationResult> PrepareAsync(WorkspaceRequest request, CancellationToken ct);
    Task CleanupAsync(WorkspaceReference workspace, CancellationToken ct);
}
