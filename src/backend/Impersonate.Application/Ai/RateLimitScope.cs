using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public enum RateLimitScope
{
    Requests,
    Tokens,
    ConcurrentRequests,
    Unknown
}
