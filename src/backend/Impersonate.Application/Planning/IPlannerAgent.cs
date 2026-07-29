using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public interface IPlannerAgent
{
    Task<PlannerAgentResult> PlanAsync(PlannerAgentRequest request, CancellationToken cancellationToken);
}
