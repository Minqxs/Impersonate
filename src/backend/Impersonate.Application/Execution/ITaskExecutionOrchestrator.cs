using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Projects;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;
using Microsoft.Extensions.Options;

namespace Impersonate.Application.Execution;

public interface ITaskExecutionOrchestrator
{
    Task<bool> ProcessOneAsync(string workerId, CancellationToken ct);
}
