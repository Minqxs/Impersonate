using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public interface ICoderAgent
{
    Task<CoderResult> ExecuteAsync(CoderContext context, CancellationToken ct);
}
