using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Ai;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Ai;

internal sealed class GeminiProviderAdapter(HttpClient http, IOptions<ExecutionOptions>? options = null, ProviderCapacityCoordinator? coordinator = null, TimeProvider? clock = null) : ProviderAdapterBase(http, options, coordinator, clock)
{
    public override ProviderType ProviderType => ProviderType.GoogleGemini;

    protected override HttpRequestMessage ModelsRequest(ProviderConnectionContext c) => new(HttpMethod.Get, $"v1beta/models?pageSize=1000&key={Uri.EscapeDataString(c.Credential.ApiKey)}");
    protected override IReadOnlyList<ProviderModel> ParseModels(JsonElement root) => root.GetProperty("models").EnumerateArray().Where(x => x.GetProperty("supportedGenerationMethods").EnumerateArray().Any(y => y.GetString() == "generateContent")).Select(x =>
    {
        var id = x.GetProperty("name").GetString()!.Replace("models/", "");
        return new ProviderModel(id, x.TryGetProperty("displayName", out var n) ? n.GetString() ?? id : id, x.TryGetProperty("description", out var d) ? d.GetString() : null, Lifecycle(id), ModelCapability.TextGeneration | ModelCapability.Reasoning | ModelCapability.StructuredOutput | ModelCapability.ToolUse, CapabilityMetadataSource.LiveProviderMetadata, x.TryGetProperty("inputTokenLimit", out var i) ? i.GetInt32() : null, x.TryGetProperty("outputTokenLimit", out var o) ? o.GetInt32() : null);
    }).ToList();
    protected override HttpRequestMessage CompletionRequest(ProviderConnectionContext c, RoutedModel m, LanguageModelRequest q)
    {
        var r = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(m.ProviderModelId)}:generateContent?key={Uri.EscapeDataString(c.Credential.ApiKey)}");
        r.Content = JsonContent.Create(new
        {
            system_instruction = new
            {
                parts = new[] { new { text = $"{q.SystemInstructions}\nOutput JSON Schema:\n{q.JsonSchema}" } }
            },
            contents = new[] { new { parts = new[] { new { text = q.UserContent } } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                maxOutputTokens = q.MaximumOutputTokens,
                temperature = 0
            }
        });
        return r;
    }

    protected override LanguageModelResponse ParseCompletion(JsonElement root, HttpResponseMessage response)
    {
        var usage = root.TryGetProperty("usageMetadata", out var u) ? u : default;
        return new(root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()!, null, usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("promptTokenCount", out var i) ? i.GetInt32() : null, usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("candidatesTokenCount", out var o) ? o.GetInt32() : null);
    }
}
