using System.Net;
using System.Text;
using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Ai;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class OpenAiResponsesParsingTests
{
    [Fact]
    public async Task Aggregates_all_output_text_items_regardless_of_order()
    {
        var response = await Complete("""{"id":"resp_1","status":"completed","output":[{"type":"reasoning","summary":[]},{"type":"message","content":[{"type":"output_text","text":"{\"a\":"},{"type":"output_text","text":"1}"}]}],"usage":{"input_tokens":11,"output_tokens":3,"output_tokens_details":{"reasoning_tokens":1}}}""");
        Assert.Equal("{\"a\":1}", response.Content);
        Assert.Equal("completed", response.ResponseStatus);
        Assert.Equal(1, response.ReasoningTokenCount);
        Assert.Null(response.SafeFailureCode);
    }

    [Fact]
    public async Task Classifies_incomplete_output_without_calling_it_malformed_json()
    {
        var response = await Complete("""{"id":"resp_2","status":"incomplete","incomplete_details":{"reason":"max_output_tokens"},"output":[],"usage":{"input_tokens":10,"output_tokens":20}}""");
        Assert.Equal("provider_output_truncated", response.SafeFailureCode);
        Assert.Equal("max_output_tokens", response.IncompleteReason);
    }

    [Fact]
    public async Task Classifies_refusal_separately()
    {
        var response = await Complete("""{"id":"resp_3","status":"completed","output":[{"type":"message","content":[{"type":"refusal","refusal":"no"}]}],"usage":{"input_tokens":10,"output_tokens":2}}""");
        Assert.Equal("provider_refused", response.SafeFailureCode);
    }

    private static async Task<LanguageModelResponse> Complete(string json)
    {
        var client = new HttpClient(new JsonHandler(json)) { BaseAddress = new("https://api.openai.test/") };
        var adapter = new OpenAiProviderAdapter(client);
        return await adapter.CompleteAsync(new(Guid.NewGuid(), ProviderType.OpenAI, new("secret")), new(null, "gpt-5"), new("gpt-5", "system", "input", "{\"type\":\"object\"}", 100), default);
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
