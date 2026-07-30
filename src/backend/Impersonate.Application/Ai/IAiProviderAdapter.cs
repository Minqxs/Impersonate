using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public interface IAiProviderAdapter
{
    ProviderType ProviderType
    {
        get;
    }

    Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext connection, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext connection, CancellationToken cancellationToken);
    Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext connection, RoutedModel model, LanguageModelRequest request, CancellationToken cancellationToken);

    Task<AgentTurnResponse> CompleteAgentTurnAsync(ProviderConnectionContext connection, RoutedModel model, AgentTurnRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{ProviderType} does not support native agent tools.");
}
