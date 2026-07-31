using Impersonate.Application.Delivery;
using Impersonate.Domain.Delivery;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfTaskDeliveryRepository(ImpersonateDbContext db) : ITaskDeliveryRepository
{
    public Task<TaskDelivery?> GetByTaskAsync(Guid projectId, Guid runId, Guid taskId, CancellationToken ct) => db.TaskDeliveries.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.PipelineRunId == runId && x.PlannedTaskId == taskId, ct);
    public async Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid projectId, Guid runId, CancellationToken ct) => await db.TaskDeliveries.Where(x => x.ProjectId == projectId && x.PipelineRunId == runId).OrderBy(x => x.TaskSequence).ToListAsync(ct);
    public Task AddAsync(TaskDelivery delivery, CancellationToken ct) => db.TaskDeliveries.AddAsync(delivery, ct).AsTask();
    public async Task<TaskDelivery?> ClaimNextPendingAsync(Guid claimId, string owner, DateTimeOffset claimedAt, DateTimeOffset expiresAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var delivery = await db.TaskDeliveries.Where(x => x.Status == TaskDeliveryStatus.Pending && (x.ClaimExpiresAtUtc == null || x.ClaimExpiresAtUtc <= claimedAt)).OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.TaskSequence).FirstOrDefaultAsync(ct);
        if (delivery is null) { await transaction.CommitAsync(ct); return null; }
        delivery.Claim(claimId, owner, expiresAt, claimedAt);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return delivery; }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return null; }
    }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
