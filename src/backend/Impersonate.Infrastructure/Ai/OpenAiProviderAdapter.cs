using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Ai;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Ai;

internal class OpenAiProviderAdapter(HttpClient http, IOptions<ExecutionOptions>? options = null, ProviderCapacityCoordinator? coordinator = null, TimeProvider? clock = null) : ProviderAdapterBase(http, options, coordinator, clock)
{
    public override ProviderType ProviderType => ProviderType.OpenAI;

    protected override HttpRequestMessage ModelsRequest(ProviderConnectionContext c) => Bearer(HttpMethod.Get, "v1/models", c.Credential.ApiKey);
    protected override IReadOnlyList<ProviderModel> ParseModels(JsonElement root) => root.GetProperty("data").EnumerateArray().Where(x => IsLanguage(x.GetProperty("id").GetString()!)).Select(x =>
    {
        var id = x.GetProperty("id").GetString()!;
        var lifecycle = id.StartsWith("gpt-3.5", StringComparison.OrdinalIgnoreCase) ? ModelLifecycleStatus.Deprecated : Lifecycle(id);
        var capabilities = ModelCapability.TextGeneration;
        if (IsReviewed(id))
        {
            capabilities |= ModelCapability.Reasoning | ModelCapability.StructuredOutput | ModelCapability.ToolUse;
            if (id.Contains("mini", StringComparison.OrdinalIgnoreCase) || id.Contains("nano", StringComparison.OrdinalIgnoreCase))
                capabilities |= ModelCapability.LowCost | ModelCapability.FastResponse;
        }

        return new ProviderModel(id, id, null, lifecycle, capabilities, IsReviewed(id) ? CapabilityMetadataSource.VersionedProviderMapping : CapabilityMetadataSource.ConservativeDefault, null, null);
    }).ToList();
    private static bool IsLanguage(string id) => id.StartsWith("gpt", StringComparison.OrdinalIgnoreCase) || id.StartsWith("o", StringComparison.OrdinalIgnoreCase);
    protected override HttpRequestMessage CompletionRequest(ProviderConnectionContext c, RoutedModel m, LanguageModelRequest q)
    {
        var responses = ProviderType == ProviderType.OpenAI && UsesResponses(m.ProviderModelId);
        var r = Bearer(HttpMethod.Post, responses ? "v1/responses" : "v1/chat/completions", c.Credential.ApiKey);
        using var schema = JsonDocument.Parse(q.JsonSchema);
        if (responses)
        {
            var gpt5 = m.ProviderModelId.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
            var format = new
            {
                type = "json_schema",
                name = "impersonate_result",
                strict = true,
                schema = schema.RootElement.Clone()
            };
            var text = new Dictionary<string, object?>
            {
                {
                    "format",
                    format
                }
            };
            if (gpt5)
                text["verbosity"] = q.TextVerbosity ?? "low";
            var body = new Dictionary<string, object?>
            {
                {
                    "model",
                    m.ProviderModelId
                },
                {
                    "instructions",
                    q.SystemInstructions
                },
                {
                    "input",
                    q.UserContent
                },
                {
                    "max_output_tokens",
                    q.MaximumOutputTokens
                },
                {
                    "text",
                    text
                }
            };
            if (gpt5 && !string.IsNullOrWhiteSpace(q.ReasoningEffort))
                body["reasoning"] = new
                {
                    effort = q.ReasoningEffort
                };
            r.Content = JsonContent.Create(body);
            return r;
        }

        var chatBody = new Dictionary<string, object?>
        {
            {
                "model",
                m.ProviderModelId
            },
            {
                "messages",
                new[]
                {
                    new
                    {
                        role = "system",
                        content = q.SystemInstructions
                    },
                    new
                    {
                        role = "user",
                        content = q.UserContent
                    }
                }
            },
            {
                "max_tokens",
                q.MaximumOutputTokens
            },
            {
                "temperature",
                0
            }
        };
        if (SupportsJsonMode(m.ProviderModelId))
            chatBody["response_format"] = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "impersonate_result",
                    strict = true,
                    schema = schema.RootElement.Clone()
                }
            };
        r.Content = JsonContent.Create(chatBody);
        return r;
    }

    private static bool IsReviewed(string id) => System.Text.RegularExpressions.Regex.IsMatch(id, @"^(gpt-(4\.1|5(?:\.\d+)?)(-(mini|nano|pro|codex))?)(-\d{4}-\d{2}-\d{2})?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(id, @"^o[34]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static bool UsesResponses(string id) => id.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) || id.StartsWith("o3", StringComparison.OrdinalIgnoreCase) || id.StartsWith("o4", StringComparison.OrdinalIgnoreCase);
    private static bool SupportsJsonMode(string id) => id.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase) || id.StartsWith("gpt-4.1", StringComparison.OrdinalIgnoreCase) || id.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) || id.StartsWith("o", StringComparison.OrdinalIgnoreCase);
    protected override LanguageModelResponse ParseCompletion(JsonElement root, HttpResponseMessage response)
    {
        var usage = root.TryGetProperty("usage", out var u) ? u : default;
        if (root.TryGetProperty("output", out var output))
        {
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            var incomplete = root.TryGetProperty("incomplete_details", out var details) && details.ValueKind == JsonValueKind.Object && details.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
            var itemTypes = new List<string>();
            var texts = new List<string>();
            var refused = false;
            foreach (var item in output.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var itemType) && itemType.ValueKind == JsonValueKind.String)
                    itemTypes.Add(itemType.GetString()!);
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var part in content.EnumerateArray())
                {
                    var type = part.TryGetProperty("type", out var partType) ? partType.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(type))
                        itemTypes.Add(type!);
                    if (type == "refusal" || part.TryGetProperty("refusal", out _))
                        refused = true;
                    if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                        texts.Add(text.GetString()!);
                }
            }

            var combined = string.Concat(texts);
            var failed = status == "failed" || root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object;
            var safeCode = failed ? "provider_response_failed" : status == "incomplete" ? "provider_output_truncated" : refused ? "provider_refused" : string.IsNullOrWhiteSpace(combined) ? "provider_missing_output" : null;
            int? reasoning = null;
            if (usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("output_tokens_details", out var outputDetails) && outputDetails.ValueKind == JsonValueKind.Object && outputDetails.TryGetProperty("reasoning_tokens", out var rt) && rt.TryGetInt32(out var reasoningValue))
                reasoning = reasoningValue;
            return new(combined, root.TryGetProperty("id", out var rid) ? rid.GetString() : null, usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("input_tokens", out var ri) ? ri.GetInt32() : null, usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("output_tokens", out var ro) ? ro.GetInt32() : null, ResponseStatus: status, IncompleteReason: incomplete, OutputItemTypes: itemTypes.Distinct().Take(20).ToList(), OutputTextLength: combined.Length, ReasoningTokenCount: reasoning, SafeFailureCode: safeCode);
        }

        return new(root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!, root.TryGetProperty("id", out var id) ? id.GetString() : null, usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("prompt_tokens", out var i) ? i.GetInt32() : null, usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("completion_tokens", out var o) ? o.GetInt32() : null, ResponseStatus: "completed");
    }
}
