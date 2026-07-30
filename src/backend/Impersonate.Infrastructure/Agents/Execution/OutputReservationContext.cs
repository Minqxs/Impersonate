using Impersonate.Application.Ai;

namespace Impersonate.Infrastructure.Agents.Execution;

internal sealed record OutputReservationContext(
    ProviderEndpoint Endpoint,
    int ModelMaximumOutputTokens,
    int EstimatedDiffTokens,
    string Phase,
    bool PatchExists,
    int PendingToolPayloadTokens = 0,
    int? PreviousOutputTokens = null,
    int? PreviousReservation = null,
    bool PriorOutputTruncated = false,
    bool ProviderResetObserved = false,
    RateLimitScope? LastRateLimitScope = null,
    long? ProviderRemainingTokens = null);
