using Impersonate.Application.Delivery;
using Impersonate.Domain.Delivery;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfRunDeliveryRepository(ImpersonateDbContext db) : IRunDeliveryRepository
{
    public Task<RunDelivery?> GetByRunAsync(Guid projectId, Guid runId, CancellationToken ct) => db.RunDeliveries.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.PipelineRunId == runId, ct);
    public Task AddAsync(RunDelivery delivery, CancellationToken ct) => db.RunDeliveries.AddAsync(delivery, ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
