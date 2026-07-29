using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record LanguageModelResponse(string Content, string? ProviderRequestId, int? InputTokenCount, int? OutputTokenCount);
