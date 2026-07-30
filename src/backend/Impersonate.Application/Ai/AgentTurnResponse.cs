namespace Impersonate.Application.Ai;

public sealed record AgentTurnResponse(
    AgentConversationReference Conversation,
    IReadOnlyList<AgentToolCall> ToolCalls,
    string? ProviderRequestId,
    int? InputTokenCount,
    int? OutputTokenCount,
    string? ResponseStatus = null,
    string? IncompleteReason = null,
    IReadOnlyList<string>? OutputItemTypes = null,
    int? ReasoningTokenCount = null,
    string? SafeFailureCode = null,
    int SameModelRequestAttemptCount = 1,
    int RateLimitRetryCount = 0,
    long CumulativeRateLimitWaitMilliseconds = 0,
    RateLimitScope? LastRateLimitScope = null,
    bool ProviderResetUsed = false,
    long? ProviderRemainingTokens = null,
    long? ProviderTokenLimit = null);
