using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ProviderCapacityMetadata(System.Net.HttpStatusCode StatusCode, string? ProviderRequestId = null, TimeSpan? RetryAfter = null, TimeSpan? RequestReset = null, TimeSpan? TokenReset = null, long? RequestLimit = null, long? RemainingRequests = null, long? TokenLimit = null, long? RemainingTokens = null, RateLimitScope Scope = RateLimitScope.Unknown, bool TemporaryCapacity = false, bool QuotaExhausted = false);
