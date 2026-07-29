using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public interface IExecutionOrderService
{
    ExecutionOrderResult Order(IReadOnlyList<PlannerTask> tasks);
}
