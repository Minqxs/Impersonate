using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Ai;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Ai;

internal abstract class ProviderAdapterBase(HttpClient http, IOptions<ExecutionOptions>? retryOptions = null, ProviderCapacityCoordinator? capacityCoordinator = null, TimeProvider? timeProvider = null) : IAiProviderAdapter
{
    protected HttpClient Http { get; } = http;

    private readonly ExecutionOptions retry = retryOptions?.Value ?? new();
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly ProviderCapacityCoordinator coordinator = capacityCoordinator ?? new(TimeProvider.System);
    public abstract ProviderType ProviderType
    {
        get;
    }

    protected abstract HttpRequestMessage ModelsRequest(ProviderConnectionContext context);
    protected abstract IReadOnlyList<ProviderModel> ParseModels(JsonElement root);
    protected abstract HttpRequestMessage CompletionRequest(ProviderConnectionContext context, RoutedModel model, LanguageModelRequest request);
    protected abstract LanguageModelResponse ParseCompletion(JsonElement root, HttpResponseMessage response);
    public virtual Task<AgentTurnResponse> CompleteAgentTurnAsync(ProviderConnectionContext context, RoutedModel model, AgentTurnRequest request, CancellationToken ct) =>
        throw new NotSupportedException($"{ProviderType} does not support native agent tools.");
    public async Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext context, CancellationToken ct)
    {
        try
        {
            using var request = ModelsRequest(context);
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(false, true, "invalid_credentials", "The provider rejected the saved credentials.");
            if (!response.IsSuccessStatusCode)
                return new(false, false, "provider_unavailable", "The provider could not be reached successfully.");
            return new(true, false, null, "Connection validated.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, false, "provider_unavailable", "The provider could not be reached successfully.");
        }
    }

    public async Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext context, CancellationToken ct)
    {
        using var request = ModelsRequest(context);
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSafeAsync(response, ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParseModels(json.RootElement);
    }

    public async Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext context, RoutedModel model, LanguageModelRequest request, CancellationToken ct)
        => await SendWithRetryAsync(context, model, () => CompletionRequest(context, model, request), async (response, token) =>
        {
            var body = await response.Content.ReadAsStringAsync(token);
            using var json = JsonDocument.Parse(body);
            return ParseCompletion(json.RootElement, response);
        }, (result, attempts, retries, waited, scope, reset) => result with
        {
            SameModelRequestAttemptCount = attempts,
            RateLimitRetryCount = retries,
            CumulativeRateLimitWaitMilliseconds = waited,
            LastRateLimitScope = scope,
            ProviderResetUsed = reset
        }, ct);

    protected async Task<T> SendWithRetryAsync<T>(ProviderConnectionContext context, RoutedModel model, Func<HttpRequestMessage> requestFactory, Func<HttpResponseMessage, CancellationToken, Task<T>> parse, Func<T, int, int, long, RateLimitScope?, bool, T> metadata, CancellationToken ct)
    {
        var family = ModelRateLimitFamily.Get(context.ProviderType, model.ProviderModelId);
        var attempts = 0;
        var retries = 0;
        long waited = 0;
        RateLimitScope? lastScope = null;
        var providerReset = false;
        while (true)
        {
            await coordinator.RespectAsync(context.ConnectionId, family, lastScope ?? RateLimitScope.Unknown, ct);
            attempts++;
            try
            {
                using var message = requestFactory();
                using var response = await Http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
                await EnsureSafeAsync(response, ct);
                var parsed = await parse(response, ct);
                return metadata(parsed, attempts, retries, waited, lastScope, providerReset);
            }
            catch (ProviderRequestException ex) when (ex.Code == "provider_rate_limited" && ex.Capacity?.TemporaryCapacity == true && retries < retry.MaximumSameModelRateLimitRetries)
            {
                var supplied = ex.Capacity.RetryAfter ?? (ex.Capacity.Scope == RateLimitScope.Tokens ? ex.Capacity.TokenReset : ex.Capacity.RequestReset);
                var backoff = Math.Min(retry.InitialRateLimitBackoffMilliseconds * Math.Pow(2, retries), retry.MaximumRateLimitBackoffSeconds * 1000);
                var jitter = retry.RateLimitJitterMaximumMilliseconds <= 0 ? 0 : Random.Shared.Next(retry.RateLimitJitterMaximumMilliseconds + 1);
                var delay = supplied ?? TimeSpan.FromMilliseconds(backoff + jitter);
                if (delay > TimeSpan.FromSeconds(retry.MaximumAutomaticRateLimitWaitSeconds) || waited + delay.TotalMilliseconds > retry.MaximumTotalRateLimitWaitSecondsPerOperation * 1000)
                    throw;
                lastScope = ex.Capacity.Scope;
                providerReset |= supplied is not null;
                coordinator.Record(context.ConnectionId, family, lastScope.Value, delay);
                await coordinator.RespectAsync(context.ConnectionId, family, lastScope.Value, ct);
                waited += (long)delay.TotalMilliseconds;
                retries++;
            }
            catch (ProviderRequestException ex) when (ex.Code == "provider_rate_limited")
            {
                var capacity = ex.Capacity is null ? null : ex.Capacity with { CumulativeWaitMilliseconds = waited };
                throw new ProviderRequestException(ex.Code, ex.Message, ex.StatusCode, ex.IsTransient, capacity);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                throw new ProviderRequestException("provider_invalid_response", "The provider returned an invalid completion response.", HttpStatusCode.OK, false);
            }
        }
    }

    protected static async Task EnsureSafeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync(ct);
        var detail = SafeProviderDetail(body);
        var quota = IsQuotaExhausted(body);
        var capacity = Capacity(response, quota);
        var (code, message, isTransient) = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ("invalid_credentials", "The provider rejected the saved credentials.", false),
            HttpStatusCode.NotFound => ("model_unavailable", "The selected provider model or endpoint was not found.", false),
            HttpStatusCode.TooManyRequests when quota => ("provider_quota_exhausted", "The provider billing quota is exhausted.", false),
            HttpStatusCode.TooManyRequests => ("provider_rate_limited", "The provider is temporarily rate limited.", true),
            HttpStatusCode.BadRequest => ("provider_request_rejected", "The provider rejected the planning request.", false),
            _ when (int)response.StatusCode >= 500 => ("provider_unavailable", "The provider is temporarily unavailable.", true),
            _ => ("provider_request_failed", "The provider request failed.", false)
        };
        var safeMessage = response.StatusCode == HttpStatusCode.TooManyRequests || string.IsNullOrWhiteSpace(detail) ? $"{message} HTTP {(int)response.StatusCode}." : $"{message} HTTP {(int)response.StatusCode}: {detail}";
        throw new ProviderRequestException(code, safeMessage, response.StatusCode, isTransient, capacity);
    }

    private static ProviderCapacityMetadata Capacity(HttpResponseMessage response, bool quota)
    {
        string? Header(string name) => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
        long? Number(string name) => long.TryParse(Header(name), out var value) ? value : null;
        var retry = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : ParseDuration(Header("Retry-After")));
        var requestReset = ParseDuration(Header("x-ratelimit-reset-requests"));
        var tokenReset = ParseDuration(Header("x-ratelimit-reset-tokens"));
        var scope = tokenReset is not null || Number("x-ratelimit-remaining-tokens") == 0 ? RateLimitScope.Tokens : requestReset is not null || Number("x-ratelimit-remaining-requests") == 0 ? RateLimitScope.Requests : RateLimitScope.Unknown;
        return new(response.StatusCode, Header("x-request-id"), retry > TimeSpan.Zero ? retry : null, requestReset, tokenReset, Number("x-ratelimit-limit-requests"), Number("x-ratelimit-remaining-requests"), Number("x-ratelimit-limit-tokens"), Number("x-ratelimit-remaining-tokens"), scope, !quota, quota);
    }

    internal static TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim().ToLowerInvariant();
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return seconds >= 0 ? TimeSpan.FromSeconds(seconds) : null;
        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"(?<n>\d+(?:\.\d+)?)(?<u>ms|s|m)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (matches.Count == 0 || string.Concat(matches.Select(x => x.Value)) != text)
            return null;
        double total = 0;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var n = double.Parse(match.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture);
            total += match.Groups["u"].Value switch
            {
                "ms" => n,
                "s" => n * 1000,
                "m" => n * 60000,
                _ => 0
            };
        }

        return TimeSpan.FromMilliseconds(total);
    }

    private static bool IsQuotaExhausted(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var error = json.RootElement.TryGetProperty("error", out var e) ? e : default;
            if (error.ValueKind != JsonValueKind.Object)
                return false;
            var code = error.TryGetProperty("code", out var c) ? c.GetString() : null;
            var type = error.TryGetProperty("type", out var t) ? t.GetString() : null;
            return code is "insufficient_quota" or "billing_hard_limit_reached" || type is "insufficient_quota" or "billing_hard_limit_reached";
        }
        catch
        {
            return false;
        }
    }

    private static string? SafeProviderDetail(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                    return Limit(message.GetString());
                if (error.ValueKind == JsonValueKind.String)
                    return Limit(error.GetString());
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string? Limit(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 300)];
    protected static ModelLifecycleStatus Lifecycle(string id) => id.Contains("preview", StringComparison.OrdinalIgnoreCase) ? ModelLifecycleStatus.Preview : id.Contains("deprecated", StringComparison.OrdinalIgnoreCase) ? ModelLifecycleStatus.Deprecated : ModelLifecycleStatus.Unknown;
    protected static string Capabilities(ModelCapability value) => JsonSerializer.Serialize((int)value);
    protected static HttpRequestMessage Bearer(HttpMethod method, string uri, string key)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new("Bearer", key);
        return request;
    }
}
