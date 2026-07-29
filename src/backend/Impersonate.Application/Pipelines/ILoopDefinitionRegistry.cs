using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public interface ILoopDefinitionRegistry
{
    LoopDefinition Get(string id, string? version = null);
}
