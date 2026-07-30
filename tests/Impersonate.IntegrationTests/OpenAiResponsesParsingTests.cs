using System.Net;
using System.Text;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Ai;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class OpenAiResponsesParsingTests
{
    [Fact]
    public async Task Native_turn_parses_function_calls_and_preserves_call_ids()
    {
        var handler = new CapturingHandler("""{"id":"resp_tools","status":"completed","output":[{"type":"reasoning","summary":[]},{"type":"function_call","call_id":"call_read","name":"read_file","arguments":"{\"path\":\"User.cs\"}"}],"usage":{"input_tokens":12,"output_tokens":4}}""");
        IAiProviderAdapter adapter = Adapter(handler);
        var turn = await adapter.CompleteAgentTurnAsync(Context(), Model(), Turn(), default);

        var call = Assert.Single(turn.ToolCalls);
        Assert.Equal("call_read", call.CallId);
        Assert.Equal("read_file", call.Name);
        Assert.Equal("{\"path\":\"User.cs\"}", call.ArgumentsJson);
        Assert.Equal("resp_tools", turn.Conversation.OpaqueId);
        using var sent = JsonDocument.Parse(handler.Bodies.Single());
        Assert.False(sent.RootElement.GetProperty("parallel_tool_calls").GetBoolean());
        Assert.Equal("required", sent.RootElement.GetProperty("tool_choice").GetString());
        Assert.True(sent.RootElement.GetProperty("tools")[0].GetProperty("strict").GetBoolean());
    }

    [Fact]
    public async Task Native_continuation_returns_matching_function_output()
    {
        var handler = new CapturingHandler("""{"id":"resp_next","status":"completed","output":[{"type":"function_call","call_id":"call_done","name":"complete_task","arguments":"{\"summary\":\"done\",\"validationNotes\":[],\"knownLimitations\":[]}"}],"usage":{"input_tokens":8,"output_tokens":3}}""");
        var adapter = Adapter(handler);
        await adapter.CompleteAgentTurnAsync(Context(), Model(), Turn(new("resp_previous"), [new("call_read", "{\"succeeded\":true}")]), default);

        using var sent = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal("resp_previous", sent.RootElement.GetProperty("previous_response_id").GetString());
        var output = sent.RootElement.GetProperty("input")[0];
        Assert.Equal("function_call_output", output.GetProperty("type").GetString());
        Assert.Equal("call_read", output.GetProperty("call_id").GetString());
    }

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

    private static OpenAiProviderAdapter Adapter(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new("https://api.openai.test/") });
    private static ProviderConnectionContext Context() => new(Guid.NewGuid(), ProviderType.OpenAI, new("secret"));
    private static RoutedModel Model() => new(null, "gpt-5");
    private static AgentTurnRequest Turn(AgentConversationReference? conversation = null, IReadOnlyList<AgentToolResult>? results = null)
    {
        using var schema = JsonDocument.Parse("""{"type":"object","additionalProperties":false,"required":["path"],"properties":{"path":{"type":"string"}}}""");
        return new("gpt-5", "system", conversation is null ? "task" : null, [new("read_file", "Read a file.", schema.RootElement.Clone())], results ?? [], conversation, 100);
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    private sealed class CapturingHandler(string json) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
