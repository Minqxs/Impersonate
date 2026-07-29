using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record ExecutionOrderResult(bool Succeeded, IReadOnlyList<OrderedPlannerTask> Tasks, IReadOnlyList<string> Errors);
