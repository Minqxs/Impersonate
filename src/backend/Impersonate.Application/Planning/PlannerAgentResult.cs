using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlannerAgentResult(PlannerPlan Plan, string? ProviderRequestId, int? InputTokenCount, int? OutputTokenCount);
