using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public interface IModelRouter
{
    Task<ModelSelectionResult> SelectAsync(ModelSelectionRequest request, CancellationToken cancellationToken);
}
