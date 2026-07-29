using System.Net;
using System.Text;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Ai;
using Microsoft.Extensions.Options;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class ProviderRateLimitRetryTests
{
    [Fact]
    public async Task OpenAi_temporary_rate_limit_retries_the_identical_model_request()
    {
        var handler = new SequenceHandler();
        var client = new HttpClient(handler) { BaseAddress = new("https://api.openai.test/") };
        var adapter = new OpenAiProviderAdapter(client);
        var request = new LanguageModelRequest("gpt-4.1", "system", "payload", "{\"type\":\"object\"}", 100);
        var response = await adapter.CompleteAsync(new(Guid.NewGuid(), ProviderType.OpenAI, new("secret")), new(null, "gpt-4.1"), request, default);
        Assert.Equal("ok", response.Content);
        Assert.Equal(2, response.SameModelRequestAttemptCount);
        Assert.Equal(1, response.RateLimitRetryCount);
        Assert.True(response.ProviderResetUsed);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, x => Assert.Contains("\"model\":\"gpt-4.1\"", x));
        Assert.Equal(handler.Requests[0], handler.Requests[1]);
    }

    [Theory]
    [InlineData("4.236", 4236)]
    [InlineData("10ms", 10)]
    [InlineData("1.5s", 1500)]
    [InlineData("1m2.5s", 62500)]
    public void Provider_duration_formats_are_parsed(string value, double milliseconds)
        => Assert.Equal(milliseconds, ProviderAdapterBase.ParseDuration(value)!.Value.TotalMilliseconds, 3);

    [Theory]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("-1s")]
    public void Malformed_provider_duration_is_ignored(string value)
        => Assert.Null(ProviderAdapterBase.ParseDuration(value));

    [Fact]
    public async Task OpenAi_quota_exhaustion_is_not_retried()
    {
        var handler = new AlwaysLimitedHandler("{\"error\":{\"type\":\"insufficient_quota\",\"code\":\"insufficient_quota\",\"message\":\"redacted provider detail\"}}", "1s");
        var adapter = new OpenAiProviderAdapter(new HttpClient(handler) { BaseAddress = new("https://api.openai.test/") });
        var ex = await Assert.ThrowsAsync<ProviderRequestException>(() => Complete(adapter));
        Assert.Equal("provider_quota_exhausted", ex.Code);
        Assert.False(ex.IsTransient);
        Assert.True(ex.Capacity!.QuotaExhausted);
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.Capacity.StatusCode);
        Assert.DoesNotContain("redacted provider detail", ex.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Reset_longer_than_ceiling_is_not_waited_or_immediately_retried()
    {
        var handler = new AlwaysLimitedHandler("{\"error\":{\"type\":\"tokens\",\"code\":\"rate_limit_exceeded\"}}", "16s");
        var options = Options.Create(new ExecutionOptions { MaximumAutomaticRateLimitWaitSeconds = 15 });
        var adapter = new OpenAiProviderAdapter(new HttpClient(handler) { BaseAddress = new("https://api.openai.test/") }, options);
        var ex = await Assert.ThrowsAsync<ProviderRequestException>(() => Complete(adapter));
        Assert.Equal("provider_rate_limited", ex.Code);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Repeated_rate_limits_are_bounded()
    {
        var handler = new AlwaysLimitedHandler("{\"error\":{\"code\":\"rate_limit_exceeded\"}}", "1ms");
        var options = Options.Create(new ExecutionOptions { MaximumSameModelRateLimitRetries = 2, RateLimitJitterMaximumMilliseconds = 0 });
        var adapter = new OpenAiProviderAdapter(new HttpClient(handler) { BaseAddress = new("https://api.openai.test/") }, options);
        await Assert.ThrowsAsync<ProviderRequestException>(() => Complete(adapter));
        Assert.Equal(3, handler.Calls);
    }

    private static Task<LanguageModelResponse> Complete(OpenAiProviderAdapter adapter) => adapter.CompleteAsync(new(Guid.NewGuid(), ProviderType.OpenAI, new("secret")), new(null, "gpt-4.1"), new("gpt-4.1", "system", "payload", "{\"type\":\"object\"}", 100), default);

    private sealed class SequenceHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (Requests.Count == 1)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{\"error\":{\"type\":\"tokens\",\"code\":\"rate_limit_exceeded\",\"message\":\"temporary\"}}", Encoding.UTF8, "application/json") };
                limited.Headers.TryAddWithoutValidation("Retry-After", "0.01");
                limited.Headers.TryAddWithoutValidation("x-ratelimit-reset-tokens", "10ms");
                limited.Headers.TryAddWithoutValidation("x-request-id", "req-limited");
                return limited;
            }
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"req-ok\",\"choices\":[{\"message\":{\"content\":\"ok\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1}}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class AlwaysLimitedHandler(string body, string retryAfter) : HttpMessageHandler
    {
        public int Calls
        {
            get; private set;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
            response.Headers.TryAddWithoutValidation("x-request-id", "req-safe");
            return Task.FromResult(response);
        }
    }
}
