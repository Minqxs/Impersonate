using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;

namespace Impersonate.Infrastructure.Ai;

internal abstract class ProviderAdapterBase(HttpClient http) : IAiProviderAdapter
{
    protected HttpClient Http { get; } = http;
    public abstract ProviderType ProviderType { get; }
    protected abstract HttpRequestMessage ModelsRequest(ProviderConnectionContext context);
    protected abstract IReadOnlyList<ProviderModel> ParseModels(JsonElement root);
    protected abstract HttpRequestMessage CompletionRequest(ProviderConnectionContext context, RoutedModel model, LanguageModelRequest request);
    protected abstract LanguageModelResponse ParseCompletion(JsonElement root, HttpResponseMessage response);
    public async Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext context, CancellationToken ct) { try { using var request = ModelsRequest(context); using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return new(false, true, "invalid_credentials", "The provider rejected the saved credentials."); if (!response.IsSuccessStatusCode) return new(false, false, "provider_unavailable", "The provider could not be reached successfully."); return new(true, false, null, "Connection validated."); } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return new(false, false, "provider_unavailable", "The provider could not be reached successfully."); } }
    public async Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext context, CancellationToken ct) { using var request = ModelsRequest(context); using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); await EnsureSafeAsync(response, ct); using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return ParseModels(json.RootElement); }
    public async Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext context, RoutedModel model, LanguageModelRequest request, CancellationToken ct) { using var message = CompletionRequest(context, model, request); using var response = await Http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct); await EnsureSafeAsync(response, ct); using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return ParseCompletion(json.RootElement, response); }
    protected static async Task EnsureSafeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body=await response.Content.ReadAsStringAsync(ct);var detail=SafeProviderDetail(body);
        var (code,message,isTransient)=response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ("invalid_credentials","The provider rejected the saved credentials.",false),
            HttpStatusCode.NotFound => ("model_unavailable","The selected provider model or endpoint was not found.",false),
            HttpStatusCode.TooManyRequests => ("provider_rate_limited","The provider rate limit or quota was exceeded.",true),
            HttpStatusCode.BadRequest => ("provider_request_rejected","The provider rejected the planning request.",false),
            _ when (int)response.StatusCode>=500 => ("provider_unavailable","The provider is temporarily unavailable.",true),
            _ => ("provider_request_failed","The provider request failed.",false)
        };
        var safeMessage=string.IsNullOrWhiteSpace(detail)?$"{message} HTTP {(int)response.StatusCode}.":$"{message} HTTP {(int)response.StatusCode}: {detail}";
        throw new ProviderRequestException(code,safeMessage,response.StatusCode,isTransient);
    }
    private static string? SafeProviderDetail(string body)
    {
        try
        {
            using var json=JsonDocument.Parse(body);var root=json.RootElement;
            if(root.TryGetProperty("error",out var error))
            {
                if(error.ValueKind==JsonValueKind.Object&&error.TryGetProperty("message",out var message))return Limit(message.GetString());
                if(error.ValueKind==JsonValueKind.String)return Limit(error.GetString());
            }
        }
        catch(JsonException){}
        return null;
    }
    private static string? Limit(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim()[..Math.Min(value.Trim().Length,300)];
    protected static ModelLifecycleStatus Lifecycle(string id) => id.Contains("preview", StringComparison.OrdinalIgnoreCase) ? ModelLifecycleStatus.Preview : id.Contains("deprecated", StringComparison.OrdinalIgnoreCase) ? ModelLifecycleStatus.Deprecated : ModelLifecycleStatus.Unknown;
    protected static string Capabilities(ModelCapability value) => JsonSerializer.Serialize((int)value);
    protected static HttpRequestMessage Bearer(HttpMethod method, string uri, string key) { var request = new HttpRequestMessage(method, uri); request.Headers.Authorization = new("Bearer", key); return request; }
}

internal sealed class AnthropicProviderAdapter(HttpClient http) : ProviderAdapterBase(http)
{
    public override ProviderType ProviderType => ProviderType.Anthropic;
    protected override HttpRequestMessage ModelsRequest(ProviderConnectionContext c) { var r = new HttpRequestMessage(HttpMethod.Get, "v1/models?limit=1000"); r.Headers.Add("x-api-key", c.Credential.ApiKey); r.Headers.Add("anthropic-version", "2023-06-01"); return r; }
    protected override IReadOnlyList<ProviderModel> ParseModels(JsonElement root) => root.GetProperty("data").EnumerateArray().Select(x => { var id=x.GetProperty("id").GetString()!; return new ProviderModel(id,x.TryGetProperty("display_name",out var n)?n.GetString()??id:id,null,Lifecycle(id),ModelCapability.TextGeneration|ModelCapability.Reasoning|ModelCapability.StructuredOutput|ModelCapability.ToolUse,CapabilityMetadataSource.VersionedProviderMapping,null,null); }).ToList();
    protected override HttpRequestMessage CompletionRequest(ProviderConnectionContext c, RoutedModel m, LanguageModelRequest q) { var r = new HttpRequestMessage(HttpMethod.Post,"v1/messages"); r.Headers.Add("x-api-key",c.Credential.ApiKey);r.Headers.Add("anthropic-version","2023-06-01");r.Content=JsonContent.Create(new{model=m.ProviderModelId,max_tokens=q.MaximumOutputTokens,temperature=0,system=q.SystemInstructions,messages=new[]{new{role="user",content=q.UserContent}}});return r; }
    protected override LanguageModelResponse ParseCompletion(JsonElement root,HttpResponseMessage response) { var usage=root.GetProperty("usage");return new(root.GetProperty("content")[0].GetProperty("text").GetString()!,root.GetProperty("id").GetString(),usage.TryGetProperty("input_tokens",out var i)?i.GetInt32():null,usage.TryGetProperty("output_tokens",out var o)?o.GetInt32():null); }
}

internal class OpenAiProviderAdapter(HttpClient http) : ProviderAdapterBase(http)
{
    public override ProviderType ProviderType => ProviderType.OpenAI;
    protected override HttpRequestMessage ModelsRequest(ProviderConnectionContext c) => Bearer(HttpMethod.Get,"v1/models",c.Credential.ApiKey);
    protected override IReadOnlyList<ProviderModel> ParseModels(JsonElement root) => root.GetProperty("data").EnumerateArray().Where(x=>IsLanguage(x.GetProperty("id").GetString()!)).Select(x=>{var id=x.GetProperty("id").GetString()!;return new ProviderModel(id,id,null,Lifecycle(id),ModelCapability.TextGeneration|ModelCapability.Reasoning|ModelCapability.StructuredOutput|ModelCapability.ToolUse,CapabilityMetadataSource.VersionedProviderMapping,null,null);}).ToList();
    private static bool IsLanguage(string id)=>id.StartsWith("gpt",StringComparison.OrdinalIgnoreCase)||id.StartsWith("o",StringComparison.OrdinalIgnoreCase);
    protected override HttpRequestMessage CompletionRequest(ProviderConnectionContext c,RoutedModel m,LanguageModelRequest q){var r=Bearer(HttpMethod.Post,"v1/chat/completions",c.Credential.ApiKey);var body=new Dictionary<string,object?>{{"model",m.ProviderModelId},{"messages",new[]{new{role="system",content=q.SystemInstructions},new{role="user",content=q.UserContent}}},{"response_format",new{type="json_object"}}};body[m.ProviderModelId.StartsWith("o",StringComparison.OrdinalIgnoreCase)?"max_completion_tokens":"max_tokens"]=q.MaximumOutputTokens;if(!m.ProviderModelId.StartsWith("o",StringComparison.OrdinalIgnoreCase))body["temperature"]=0;r.Content=JsonContent.Create(body);return r;}
    protected override LanguageModelResponse ParseCompletion(JsonElement root,HttpResponseMessage response){var usage=root.TryGetProperty("usage",out var u)?u:default;return new(root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!,root.TryGetProperty("id",out var id)?id.GetString():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("prompt_tokens",out var i)?i.GetInt32():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("completion_tokens",out var o)?o.GetInt32():null);}
}

internal sealed class OpenRouterProviderAdapter(HttpClient http) : OpenAiProviderAdapter(http)
{
    public override ProviderType ProviderType => ProviderType.OpenRouter;
}

internal sealed class GeminiProviderAdapter(HttpClient http) : ProviderAdapterBase(http)
{
    public override ProviderType ProviderType => ProviderType.GoogleGemini;
    protected override HttpRequestMessage ModelsRequest(ProviderConnectionContext c)=>new(HttpMethod.Get,$"v1beta/models?pageSize=1000&key={Uri.EscapeDataString(c.Credential.ApiKey)}");
    protected override IReadOnlyList<ProviderModel> ParseModels(JsonElement root)=>root.GetProperty("models").EnumerateArray().Where(x=>x.GetProperty("supportedGenerationMethods").EnumerateArray().Any(y=>y.GetString()=="generateContent")).Select(x=>{var id=x.GetProperty("name").GetString()!.Replace("models/","");return new ProviderModel(id,x.TryGetProperty("displayName",out var n)?n.GetString()??id:id,x.TryGetProperty("description",out var d)?d.GetString():null,Lifecycle(id),ModelCapability.TextGeneration|ModelCapability.Reasoning|ModelCapability.StructuredOutput|ModelCapability.ToolUse,CapabilityMetadataSource.LiveProviderMetadata,x.TryGetProperty("inputTokenLimit",out var i)?i.GetInt32():null,x.TryGetProperty("outputTokenLimit",out var o)?o.GetInt32():null);}).ToList();
    protected override HttpRequestMessage CompletionRequest(ProviderConnectionContext c,RoutedModel m,LanguageModelRequest q){var r=new HttpRequestMessage(HttpMethod.Post,$"v1beta/models/{Uri.EscapeDataString(m.ProviderModelId)}:generateContent?key={Uri.EscapeDataString(c.Credential.ApiKey)}");r.Content=JsonContent.Create(new{system_instruction=new{parts=new[]{new{text=q.SystemInstructions}}},contents=new[]{new{parts=new[]{new{text=q.UserContent}}}},generationConfig=new{responseMimeType="application/json",maxOutputTokens=q.MaximumOutputTokens,temperature=0}});return r;}
    protected override LanguageModelResponse ParseCompletion(JsonElement root,HttpResponseMessage response){var usage=root.TryGetProperty("usageMetadata",out var u)?u:default;return new(root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()!,null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("promptTokenCount",out var i)?i.GetInt32():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("candidatesTokenCount",out var o)?o.GetInt32():null);}
}
