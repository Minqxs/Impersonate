using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Impersonate.Application.Planning;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Agents.Planner;

internal sealed class AnthropicLanguageModelClient(HttpClient http) : ILanguageModelClient
{
    public async Task<Impersonate.Application.Planning.LanguageModelResponse> CompleteAsync(Impersonate.Application.Planning.LanguageModelRequest request, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/messages");
        message.Headers.Add("anthropic-version", "2023-06-01");
        message.Content = JsonContent.Create(new
        {
            model = request.Model,
            max_tokens = request.MaximumOutputTokens,
            temperature = 0,
            system = $"{request.SystemInstructions}\nOutput JSON Schema:\n{request.JsonSchema}",
            messages = new[] { new { role = "user", content = request.UserContent } }
        });
        using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic request failed with status {(int)response.StatusCode}.");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var content = root.GetProperty("content")[0].GetProperty("text").GetString() ?? throw new InvalidDataException("Anthropic returned empty content.");
        var usage = root.TryGetProperty("usage", out var u) ? u : default;
        return new(content, root.TryGetProperty("id", out var id) ? id.GetString() : null, usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("input_tokens", out var i) ? i.GetInt32() : null, usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("output_tokens", out var o) ? o.GetInt32() : null);
    }
}
