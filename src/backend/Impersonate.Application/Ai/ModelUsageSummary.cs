using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ModelUsageSummary(string Provider, string Model, int AttemptCount, int SuccessfulPlanCount, int InvalidOutputCount, int ProviderFailureCount, int TimedOutCount, long InputTokenCount, long OutputTokenCount, double AverageDurationMilliseconds, double ValidPlanRate);
