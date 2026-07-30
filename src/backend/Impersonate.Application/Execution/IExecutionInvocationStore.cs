using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public interface IExecutionInvocationStore
{
    Task<Guid?> FindLatestSelectionDecisionIdAsync(Guid taskAttemptId, AgentRole role, CancellationToken ct);
    Task AddAsync(ExecutionInvocation invocation, CancellationToken ct);
    Task<IReadOnlyList<ExecutionInvocation>> ListAsync(IReadOnlyCollection<Guid> taskAttemptIds, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
