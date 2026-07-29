using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed class ProviderRequestException(string code, string safeMessage, System.Net.HttpStatusCode statusCode, bool isTransient, ProviderCapacityMetadata? capacity = null) : Exception(safeMessage)
{
    public string Code { get; } = code;
    public System.Net.HttpStatusCode StatusCode { get; } = statusCode;
    public bool IsTransient { get; } = isTransient;
    public ProviderCapacityMetadata? Capacity { get; } = capacity;
}
