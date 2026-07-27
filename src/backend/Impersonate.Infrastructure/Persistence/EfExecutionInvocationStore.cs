using Impersonate.Application.Execution;
using Impersonate.Domain.Pipelines;
using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfExecutionInvocationStore(ImpersonateDbContext db):IExecutionInvocationStore
{
    public Task<Guid?> FindLatestSelectionDecisionIdAsync(Guid taskAttemptId,Impersonate.Domain.Ai.AgentRole role,CancellationToken ct)=>db.ModelSelectionDecisions.AsNoTracking().Where(x=>x.TaskAttemptId==taskAttemptId&&x.Role==role).OrderByDescending(x=>x.CreatedAtUtc).Select(x=>(Guid?)x.Id).FirstOrDefaultAsync(ct);
    public Task AddAsync(ExecutionInvocation invocation,CancellationToken ct)=>db.ExecutionInvocations.AddAsync(invocation,ct).AsTask();
    public async Task<IReadOnlyList<ExecutionInvocation>> ListAsync(IReadOnlyCollection<Guid> taskAttemptIds,CancellationToken ct)=>await db.ExecutionInvocations.AsNoTracking().Where(x=>taskAttemptIds.Contains(x.TaskAttemptId)).OrderBy(x=>x.TaskAttemptId).ThenBy(x=>x.Sequence).ToListAsync(ct);
    public Task SaveChangesAsync(CancellationToken ct)=>db.SaveChangesAsync(ct);
}
