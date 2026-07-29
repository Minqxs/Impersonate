using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Ai;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Ai;

internal sealed class ProviderCapacityCoordinator(TimeProvider clock)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> cooldowns = new();
    public void Record(Guid connection, string family, RateLimitScope scope, TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            return;
        cooldowns.AddOrUpdate($"{connection:N}:{family}:{scope}", clock.GetUtcNow() + delay, (_, old) => old > clock.GetUtcNow() + delay ? old : clock.GetUtcNow() + delay);
    }

    public async Task RespectAsync(Guid connection, string family, RateLimitScope scope, CancellationToken ct)
    {
        var prefix = $"{connection:N}:{family}:";
        var candidates = scope == RateLimitScope.Unknown ? cooldowns.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)).ToArray() : cooldowns.Where(x => x.Key == prefix + scope || x.Key == prefix + RateLimitScope.Unknown).ToArray();
        if (candidates.Length == 0)
            return;
        var until = candidates.Max(x => x.Value);
        var remaining = until - clock.GetUtcNow();
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining, clock, ct);
        foreach (var candidate in candidates.Where(x => x.Value <= clock.GetUtcNow()))
            cooldowns.TryRemove(candidate.Key, out _);
    }
}
