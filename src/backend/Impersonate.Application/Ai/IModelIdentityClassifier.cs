using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public interface IModelIdentityClassifier
{
    ModelIdentity Classify(ProviderType provider, string modelId);
}
