using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record LanguageModelResponse(string Content, string? ProviderRequestId, int? InputTokenCount, int? OutputTokenCount, int SameModelRequestAttemptCount = 1, int RateLimitRetryCount = 0, long CumulativeRateLimitWaitMilliseconds = 0, RateLimitScope? LastRateLimitScope = null, bool ProviderResetUsed = false, string? ResponseStatus = null, string? IncompleteReason = null, IReadOnlyList<string>? OutputItemTypes = null, int OutputTextLength = 0, int? ReasoningTokenCount = null, string? SafeFailureCode = null);
