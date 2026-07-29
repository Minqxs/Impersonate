using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlannerPlan(string Summary, bool CanPlan, IReadOnlyList<string> PlanningNotes, IReadOnlyList<PlannerTask> Tasks, string? FailureReason, string? ClarifyingQuestion);
