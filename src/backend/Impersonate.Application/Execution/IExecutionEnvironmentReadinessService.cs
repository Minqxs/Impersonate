using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public interface IExecutionEnvironmentReadinessService
{
    Task<ExecutionEnvironmentReadiness> CheckAsync(CancellationToken ct);
}
