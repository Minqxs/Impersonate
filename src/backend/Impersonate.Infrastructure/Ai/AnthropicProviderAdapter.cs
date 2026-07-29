using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Ai;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Ai;

internal sealed class AnthropicProviderAdapter(HttpClient http, IOptions<ExecutionOptions>? options = null, ProviderCapacityCoordinator? coordinator = null, TimeProvider? clock = null) : ProviderAdapterBase(http, options, coordinator, clock)
{
    public override ProviderType ProviderType => ProviderType.Anthropic;

    protected override HttpRequestMessage ModelsRequest(ProviderConnectionContext c)
    {
        var r = new HttpRequestMessage(HttpMethod.Get, "v1/models?limit=1000");
        r.Headers.Add("x-api-key", c.Credential.ApiKey);
        r.Headers.Add("anthropic-version", "2023-06-01");
        return r;
    }

    protected override IReadOnlyList<ProviderModel> ParseModels(JsonElement root) => root.GetProperty("data").EnumerateArray().Select(x =>
    {
        var id = x.GetProperty("id").GetString()!;
        return new ProviderModel(id, x.TryGetProperty("display_name", out var n) ? n.GetString() ?? id : id, null, Lifecycle(id), ModelCapability.TextGeneration | ModelCapability.Reasoning | ModelCapability.StructuredOutput | ModelCapability.ToolUse, CapabilityMetadataSource.VersionedProviderMapping, null, null);
    }).ToList();
    protected override HttpRequestMessage CompletionRequest(ProviderConnectionContext c, RoutedModel m, LanguageModelRequest q)
    {
        var r = new HttpRequestMessage(HttpMethod.Post, "v1/messages");
        r.Headers.Add("x-api-key", c.Credential.ApiKey);
        r.Headers.Add("anthropic-version", "2023-06-01");
        r.Content = JsonContent.Create(new
        {
            model = m.ProviderModelId,
            max_tokens = q.MaximumOutputTokens,
            temperature = 0,
            system = $"{q.SystemInstructions}\nOutput JSON Schema:\n{q.JsonSchema}",
            messages = new[] { new { role = "user", content = q.UserContent } }
        });
        return r;
    }

    protected override LanguageModelResponse ParseCompletion(JsonElement root, HttpResponseMessage response)
    {
        var usage = root.GetProperty("usage");
        return new(root.GetProperty("content")[0].GetProperty("text").GetString()!, root.GetProperty("id").GetString(), usage.TryGetProperty("input_tokens", out var i) ? i.GetInt32() : null, usage.TryGetProperty("output_tokens", out var o) ? o.GetInt32() : null);
    }
}
