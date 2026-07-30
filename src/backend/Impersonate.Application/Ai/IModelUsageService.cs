using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public interface IModelUsageService
{
    Task<IReadOnlyList<ModelUsageSummary>> GetPlanningUsageAsync(int days, CancellationToken cancellationToken);
}
