using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public interface ILanguageModelClient
{
    Task<LanguageModelResponse> CompleteAsync(LanguageModelRequest request, CancellationToken cancellationToken);
}
