using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Ai;

internal sealed class ModelUsageService(ImpersonateDbContext db) : IModelUsageService
{
    public async Task<IReadOnlyList<ModelUsageSummary>> GetPlanningUsageAsync(int days, CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(days, 1, 365));
        var attempts = await db.PlanningAttempts.AsNoTracking().Where(x => x.StartedAtUtc >= since && x.Status != PlanningAttemptStatus.Started).ToListAsync(ct);
        return attempts.GroupBy(x => new { x.Provider, x.Model }).Select(group =>
        {
            var completed = group.Where(x => x.CompletedAtUtc is not null).ToList();
            var successes = group.Count(x => x.Status == PlanningAttemptStatus.Succeeded);
            return new ModelUsageSummary(group.Key.Provider, group.Key.Model, group.Count(), successes,
                group.Count(x => x.Status == PlanningAttemptStatus.InvalidOutput), group.Count(x => x.Status == PlanningAttemptStatus.ProviderFailed),
                group.Count(x => x.Status == PlanningAttemptStatus.TimedOut), group.Sum(x => (long)(x.InputTokenCount ?? 0)),
                group.Sum(x => (long)(x.OutputTokenCount ?? 0)), completed.Count == 0 ? 0 : completed.Average(x => (x.CompletedAtUtc!.Value - x.StartedAtUtc).TotalMilliseconds),
                successes * 100d / group.Count());
        }).OrderByDescending(x => x.ValidPlanRate).ThenBy(x => x.OutputTokenCount).ThenBy(x => x.Provider).ThenBy(x => x.Model).ToList();
    }
}
