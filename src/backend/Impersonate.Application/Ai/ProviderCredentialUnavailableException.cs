using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed class ProviderCredentialUnavailableException(string code, string safeMessage) : Exception(safeMessage)
{
    public string Code { get; } = code;
}
