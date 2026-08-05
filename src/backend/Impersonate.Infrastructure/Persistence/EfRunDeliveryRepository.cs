using Impersonate.Application.Delivery;
using Impersonate.Domain.Delivery;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfRunDeliveryRepository(ImpersonateDbContext db) : IRunDeliveryRepository
{
    public Task<RunDelivery?> GetByRunAsync(Guid projectId, Guid runId, CancellationToken ct) => db.RunDeliveries.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.PipelineRunId == runId, ct);
    public Task AddAsync(RunDelivery delivery, CancellationToken ct) => db.RunDeliveries.AddAsync(delivery, ct).AsTask();
    public async Task<RunDelivery?> ClaimNextFinalReviewAsync(Guid claimId, string owner, DateTimeOffset at, DateTimeOffset expiresAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var delivery = await db.RunDeliveries.Where(x => (x.Status == RunDeliveryStatus.IntegratingTasks || x.Status == RunDeliveryStatus.AggregateValidation || x.Status == RunDeliveryStatus.FinalReview || x.Status == RunDeliveryStatus.ChangesRequested) && (x.ClaimExpiresAtUtc == null || x.ClaimExpiresAtUtc <= at)).OrderBy(x => x.UpdatedAtUtc).FirstOrDefaultAsync(ct);
        if (delivery is null)
        {
            await transaction.CommitAsync(ct);
            return null;
        }
        delivery.Claim(claimId, owner, expiresAt, at);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return delivery;
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return null; }
    }
    public async Task<RunDelivery?> ClaimNextFinalPullRequestAsync(Guid claimId, string owner, DateTimeOffset at, DateTimeOffset expiresAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var delivery = await db.RunDeliveries.Where(x => (x.Status == RunDeliveryStatus.ReadyForFinalPullRequest || x.Status == RunDeliveryStatus.FinalPullRequestOpen) && (x.ClaimExpiresAtUtc == null || x.ClaimExpiresAtUtc <= at)).OrderBy(x => x.UpdatedAtUtc).FirstOrDefaultAsync(ct);
        if (delivery is null)
        {
            await transaction.CommitAsync(ct);
            return null;
        }
        delivery.Claim(claimId, owner, expiresAt, at);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return delivery;
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return null; }
    }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
