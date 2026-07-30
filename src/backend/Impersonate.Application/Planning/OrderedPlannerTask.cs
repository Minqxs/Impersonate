using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record OrderedPlannerTask(PlannerTask Task, int OriginalSequence, int ExecutionSequence, bool OrderAdjusted, string? AdjustmentReason);
