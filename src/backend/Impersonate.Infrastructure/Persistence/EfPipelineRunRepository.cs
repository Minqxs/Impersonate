using Impersonate.Application.Pipelines;
using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;
namespace Impersonate.Infrastructure.Persistence;
internal sealed class EfPipelineRunRepository(ImpersonateDbContext db):IPipelineRunRepository
{
 public Task AddAsync(PipelineRun run,CancellationToken ct)=>db.PipelineRuns.AddAsync(run,ct).AsTask();
 public Task<PipelineRun?> GetAsync(Guid projectId,Guid runId,CancellationToken ct)=>db.PipelineRuns.Include(x=>x.LoopRun).Include(x=>x.Tasks).ThenInclude(x=>x.Attempts).Include(x=>x.Tasks).ThenInclude(x=>x.ReviewDecisions).Include(x=>x.Events).SingleOrDefaultAsync(x=>x.ProjectId==projectId&&x.Id==runId,ct);
 public async Task<IReadOnlyList<PipelineRun>> ListAsync(Guid projectId,PipelineRunStatus? status,DateTimeOffset? from,DateTimeOffset? to,CancellationToken ct){var q=db.PipelineRuns.AsNoTracking().Include(x=>x.LoopRun).Include(x=>x.Tasks).ThenInclude(x=>x.Attempts).Include(x=>x.Tasks).ThenInclude(x=>x.ReviewDecisions).Where(x=>x.ProjectId==projectId);if(status is not null)q=q.Where(x=>x.Status==status);if(from is not null)q=q.Where(x=>x.CreatedAtUtc>=from);if(to is not null)q=q.Where(x=>x.CreatedAtUtc<=to);return await q.OrderByDescending(x=>x.CreatedAtUtc).ThenByDescending(x=>x.Id).ToListAsync(ct);}
 public Task SaveChangesAsync(CancellationToken ct)=>db.SaveChangesAsync(ct);
}
