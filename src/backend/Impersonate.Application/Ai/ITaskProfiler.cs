using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public interface ITaskProfiler
{
    TaskProfile Profile(AgentRole role, string description);
    TaskProfile Profile(ModelSelectionRequest request);
}
