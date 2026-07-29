using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record PipelineOptions
{
    public int MaximumRevisionAttempts { get; init; } = 3;
    public bool ContinueOnTaskFailure { get; init; } = true;
}
