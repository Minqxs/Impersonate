using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public interface IModelCapabilityCatalog
{
    ModelCapabilityProfile Resolve(ProviderType provider, string modelId);
}
