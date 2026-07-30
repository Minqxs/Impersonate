using Impersonate.Application.Delivery;
using Impersonate.Domain.Delivery;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfTaskDeliveryRepository(ImpersonateDbContext db) : ITaskDeliveryRepository
{
    public Task<TaskDelivery?> GetByTaskAsync(Guid projectId, Guid runId, Guid taskId, CancellationToken ct) => db.TaskDeliveries.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.PipelineRunId == runId && x.PlannedTaskId == taskId, ct);
    public async Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid projectId, Guid runId, CancellationToken ct) => await db.TaskDeliveries.Where(x => x.ProjectId == projectId && x.PipelineRunId == runId).OrderBy(x => x.TaskSequence).ToListAsync(ct);
    public Task AddAsync(TaskDelivery delivery, CancellationToken ct) => db.TaskDeliveries.AddAsync(delivery, ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
