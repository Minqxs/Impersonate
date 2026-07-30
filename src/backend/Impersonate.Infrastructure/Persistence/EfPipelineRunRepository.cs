using Impersonate.Application.Pipelines;
using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfPipelineRunRepository(ImpersonateDbContext db) : IPipelineRunRepository
{
    public Task AddAsync(PipelineRun run, CancellationToken ct) => db.PipelineRuns.AddAsync(run, ct).AsTask();
    public Task<PipelineRun?> GetAsync(Guid projectId, Guid runId, CancellationToken ct) => db.PipelineRuns.AsSplitQuery().Include(x => x.LoopRun).Include(x => x.Tasks).ThenInclude(x => x.Attempts).Include(x => x.Tasks).ThenInclude(x => x.ReviewDecisions).Include(x => x.Deliveries).Include(x => x.Events).SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == runId, ct);
    public async Task<PipelineRun?> ClaimNextExecutionAsync(Guid claimId, string workerId, DateTimeOffset claimedAt, DateTimeOffset expiresAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var run = await db.PipelineRuns.AsSplitQuery().Include(x => x.LoopRun).Include(x => x.Tasks).ThenInclude(x => x.Attempts).Include(x => x.Tasks).ThenInclude(x => x.ReviewDecisions).Include(x => x.Events).Where(x => x.Status == PipelineRunStatus.Executing && (x.ExecutionClaimExpiresAtUtc == null || x.ExecutionClaimExpiresAtUtc <= claimedAt)).OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (run is null)
        {
            await transaction.CommitAsync(ct);
            return null;
        }
        try
        {
            run.ClaimNextTask(claimId, workerId, expiresAt, claimedAt);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return run;
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return null; }
    }
    public async Task<IReadOnlyList<PlanningAttempt>> GetPlanningAttemptsAsync(Guid runId, CancellationToken ct) => await db.PlanningAttempts.AsNoTracking().Where(x => x.PipelineRunId == runId).OrderBy(x => x.AttemptNumber).ToListAsync(ct);
    public async Task<IReadOnlyList<PipelineRun>> ListAsync(Guid projectId, PipelineRunStatus? status, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var q = db.PipelineRuns.AsNoTracking().AsSplitQuery().Include(x => x.LoopRun).Include(x => x.Tasks).ThenInclude(x => x.Attempts).Include(x => x.Tasks).ThenInclude(x => x.ReviewDecisions).Include(x => x.Deliveries).Where(x => x.ProjectId == projectId);
        if (status is not null)
            q = q.Where(x => x.Status == status);
        if (from is not null)
            q = q.Where(x => x.CreatedAtUtc >= from);
        if (to is not null)
            q = q.Where(x => x.CreatedAtUtc <= to);
        return await q.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).ToListAsync(ct);
    }
    public async Task DeleteAsync(Guid projectId, Guid runId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var taskIds = db.Set<PlannedTask>().Where(x => x.PipelineRunId == runId).Select(x => x.Id);
        var attemptIds = db.TaskAttempts.Where(x => taskIds.Contains(x.PlannedTaskId)).Select(x => x.Id);
        await db.TaskDeliveries.Where(x => x.ProjectId == projectId && x.PipelineRunId == runId).ExecuteDeleteAsync(ct);
        await db.ExecutionInvocations.Where(x => attemptIds.Contains(x.TaskAttemptId)).ExecuteDeleteAsync(ct);
        await db.ModelSelectionDecisions.Where(x => x.ProjectId == projectId && (x.PipelineRunId == runId || x.PlannedTaskId != null && taskIds.Contains(x.PlannedTaskId.Value))).ExecuteDeleteAsync(ct);
        await db.ReviewDecisions.Where(x => taskIds.Contains(x.PlannedTaskId)).ExecuteDeleteAsync(ct);
        await db.TaskAttempts.Where(x => taskIds.Contains(x.PlannedTaskId)).ExecuteDeleteAsync(ct);
        await db.Set<PlannedTask>().Where(x => x.PipelineRunId == runId).ExecuteDeleteAsync(ct);
        await db.PlanningAttempts.Where(x => x.PipelineRunId == runId).ExecuteDeleteAsync(ct);
        await db.Set<PipelineRunEvent>().Where(x => x.PipelineRunId == runId).ExecuteDeleteAsync(ct);
        await db.Set<LoopRun>().Where(x => x.PipelineRunId == runId).ExecuteDeleteAsync(ct);
        await db.PipelineRuns.Where(x => x.ProjectId == projectId && x.Id == runId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }
    public void RemoveTransientAttempt(TaskAttempt attempt)
    {
        if (!attempt.IsUnstartedTransientAttempt)
            throw new InvalidOperationException("Only an unstarted transient task attempt can be removed.");
        var autoDetectChanges = db.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            var entry = db.Entry(attempt);
            if (entry.State == EntityState.Detached)
                throw new InvalidOperationException("The transient task attempt must be tracked by this unit of work.");
            entry.State = EntityState.Deleted;
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
    }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
