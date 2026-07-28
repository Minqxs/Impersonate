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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,DateTimeOffset> cooldowns=new();
    public void Record(Guid connection,string family,RateLimitScope scope,TimeSpan delay){if(delay<=TimeSpan.Zero)return;cooldowns.AddOrUpdate($"{connection:N}:{family}:{scope}",clock.GetUtcNow()+delay,(_,old)=>old>clock.GetUtcNow()+delay?old:clock.GetUtcNow()+delay);}
    public async Task RespectAsync(Guid connection,string family,RateLimitScope scope,CancellationToken ct)
    {
        var prefix=$"{connection:N}:{family}:";
        var candidates=scope==RateLimitScope.Unknown
            ? cooldowns.Where(x=>x.Key.StartsWith(prefix,StringComparison.Ordinal)).ToArray()
            : cooldowns.Where(x=>x.Key==prefix+scope||x.Key==prefix+RateLimitScope.Unknown).ToArray();
        if(candidates.Length==0)return;
        var until=candidates.Max(x=>x.Value);var remaining=until-clock.GetUtcNow();
        if(remaining>TimeSpan.Zero)await Task.Delay(remaining,clock,ct);
        foreach(var candidate in candidates.Where(x=>x.Value<=clock.GetUtcNow()))cooldowns.TryRemove(candidate.Key,out _);
    }
}

internal abstract class ProviderAdapterBase(HttpClient http,IOptions<ExecutionOptions>? retryOptions=null,ProviderCapacityCoordinator? capacityCoordinator=null,TimeProvider? timeProvider=null) : IAiProviderAdapter
{
    protected HttpClient Http { get; } = http;
    private readonly ExecutionOptions retry=retryOptions?.Value??new();private readonly TimeProvider clock=timeProvider??TimeProvider.System;private readonly ProviderCapacityCoordinator coordinator=capacityCoordinator??new(TimeProvider.System);
    public abstract ProviderType ProviderType { get; }
    protected abstract HttpRequestMessage ModelsRequest(ProviderConnectionContext context);
    protected abstract IReadOnlyList<ProviderModel> ParseModels(JsonElement root);
    protected abstract HttpRequestMessage CompletionRequest(ProviderConnectionContext context, RoutedModel model, LanguageModelRequest request);
    protected abstract LanguageModelResponse ParseCompletion(JsonElement root, HttpResponseMessage response);
    public async Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext context, CancellationToken ct) { try { using var request = ModelsRequest(context); using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return new(false, true, "invalid_credentials", "The provider rejected the saved credentials."); if (!response.IsSuccessStatusCode) return new(false, false, "provider_unavailable", "The provider could not be reached successfully."); return new(true, false, null, "Connection validated."); } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return new(false, false, "provider_unavailable", "The provider could not be reached successfully."); } }
    public async Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext context, CancellationToken ct) { using var request = ModelsRequest(context); using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); await EnsureSafeAsync(response, ct); using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return ParseModels(json.RootElement); }
    public async Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext context, RoutedModel model, LanguageModelRequest request, CancellationToken ct)
    {
        var family=ModelRateLimitFamily.Get(context.ProviderType,model.ProviderModelId);var attempts=0;var retries=0;long waited=0;RateLimitScope? lastScope=null;var providerReset=false;
        while(true){await coordinator.RespectAsync(context.ConnectionId,family,lastScope??RateLimitScope.Unknown,ct);attempts++;try{using var message=CompletionRequest(context,model,request);using var response=await Http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,ct);await EnsureSafeAsync(response,ct);var body=await response.Content.ReadAsStringAsync(ct);using var json=JsonDocument.Parse(body);var parsed=ParseCompletion(json.RootElement,response);return parsed with{SameModelRequestAttemptCount=attempts,RateLimitRetryCount=retries,CumulativeRateLimitWaitMilliseconds=waited,LastRateLimitScope=lastScope,ProviderResetUsed=providerReset};}
        catch(ProviderRequestException ex) when(ex.Code=="provider_rate_limited"&&ex.Capacity?.TemporaryCapacity==true&&retries<retry.MaximumSameModelRateLimitRetries){var supplied=ex.Capacity.RetryAfter??(ex.Capacity.Scope==RateLimitScope.Tokens?ex.Capacity.TokenReset:ex.Capacity.RequestReset);var backoff=Math.Min(retry.InitialRateLimitBackoffMilliseconds*Math.Pow(2,retries),retry.MaximumRateLimitBackoffSeconds*1000);var jitter=retry.RateLimitJitterMaximumMilliseconds<=0?0:Random.Shared.Next(retry.RateLimitJitterMaximumMilliseconds+1);var delay=supplied??TimeSpan.FromMilliseconds(backoff+jitter);if(delay>TimeSpan.FromSeconds(retry.MaximumAutomaticRateLimitWaitSeconds)||waited+delay.TotalMilliseconds>retry.MaximumTotalRateLimitWaitSecondsPerOperation*1000)throw;lastScope=ex.Capacity.Scope;providerReset|=supplied is not null;coordinator.Record(context.ConnectionId,family,lastScope.Value,delay);await coordinator.RespectAsync(context.ConnectionId,family,lastScope.Value,ct);waited+=(long)delay.TotalMilliseconds;retries++;}
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException){throw new ProviderRequestException("provider_invalid_response","The provider returned an invalid completion response.",HttpStatusCode.OK,false);}}
    }
    protected static async Task EnsureSafeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body=await response.Content.ReadAsStringAsync(ct);var detail=SafeProviderDetail(body);var quota=IsQuotaExhausted(body);var capacity=Capacity(response,quota);
        var (code,message,isTransient)=response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ("invalid_credentials","The provider rejected the saved credentials.",false),
            HttpStatusCode.NotFound => ("model_unavailable","The selected provider model or endpoint was not found.",false),
            HttpStatusCode.TooManyRequests when quota => ("provider_quota_exhausted","The provider billing quota is exhausted.",false),
            HttpStatusCode.TooManyRequests => ("provider_rate_limited","The provider is temporarily rate limited.",true),
            HttpStatusCode.BadRequest => ("provider_request_rejected","The provider rejected the planning request.",false),
            _ when (int)response.StatusCode>=500 => ("provider_unavailable","The provider is temporarily unavailable.",true),
            _ => ("provider_request_failed","The provider request failed.",false)
        };
        var safeMessage=response.StatusCode==HttpStatusCode.TooManyRequests||string.IsNullOrWhiteSpace(detail)?$"{message} HTTP {(int)response.StatusCode}.":$"{message} HTTP {(int)response.StatusCode}: {detail}";
        throw new ProviderRequestException(code,safeMessage,response.StatusCode,isTransient,capacity);
    }
    private static ProviderCapacityMetadata Capacity(HttpResponseMessage response,bool quota){string? Header(string name)=>response.Headers.TryGetValues(name,out var values)?values.FirstOrDefault():null;long? Number(string name)=>long.TryParse(Header(name),out var value)?value:null;var retry=response.Headers.RetryAfter?.Delta??(response.Headers.RetryAfter?.Date is{} date?date-DateTimeOffset.UtcNow:ParseDuration(Header("Retry-After")));var requestReset=ParseDuration(Header("x-ratelimit-reset-requests"));var tokenReset=ParseDuration(Header("x-ratelimit-reset-tokens"));var scope=tokenReset is not null||Number("x-ratelimit-remaining-tokens")==0?RateLimitScope.Tokens:requestReset is not null||Number("x-ratelimit-remaining-requests")==0?RateLimitScope.Requests:RateLimitScope.Unknown;return new(response.StatusCode,Header("x-request-id"),retry>TimeSpan.Zero?retry:null,requestReset,tokenReset,Number("x-ratelimit-limit-requests"),Number("x-ratelimit-remaining-requests"),Number("x-ratelimit-limit-tokens"),Number("x-ratelimit-remaining-tokens"),scope,!quota,quota);}
    internal static TimeSpan? ParseDuration(string? value){if(string.IsNullOrWhiteSpace(value))return null;var text=value.Trim().ToLowerInvariant();if(double.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var seconds))return seconds>=0?TimeSpan.FromSeconds(seconds):null;var matches=System.Text.RegularExpressions.Regex.Matches(text,@"(?<n>\d+(?:\.\d+)?)(?<u>ms|s|m)",System.Text.RegularExpressions.RegexOptions.CultureInvariant);if(matches.Count==0||string.Concat(matches.Select(x=>x.Value))!=text)return null;double total=0;foreach(System.Text.RegularExpressions.Match match in matches){var n=double.Parse(match.Groups["n"].Value,System.Globalization.CultureInfo.InvariantCulture);total+=match.Groups["u"].Value switch{"ms"=>n,"s"=>n*1000,"m"=>n*60000,_=>0};}return TimeSpan.FromMilliseconds(total);}
    private static bool IsQuotaExhausted(string body){try{using var json=JsonDocument.Parse(body);var error=json.RootElement.TryGetProperty("error",out var e)?e:default;if(error.ValueKind!=JsonValueKind.Object)return false;var code=error.TryGetProperty("code",out var c)?c.GetString():null;var type=error.TryGetProperty("type",out var t)?t.GetString():null;return code is "insufficient_quota" or "billing_hard_limit_reached"||type is "insufficient_quota" or "billing_hard_limit_reached";}catch{return false;}}
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

internal sealed class AnthropicProviderAdapter(HttpClient http,IOptions<ExecutionOptions>? options=null,ProviderCapacityCoordinator? coordinator=null,TimeProvider? clock=null) : ProviderAdapterBase(http,options,coordinator,clock)
{
    public override ProviderType ProviderType => ProviderType.Anthropic;
    protected override HttpRequestMessage ModelsRequest(ProviderConnectionContext c) { var r = new HttpRequestMessage(HttpMethod.Get, "v1/models?limit=1000"); r.Headers.Add("x-api-key", c.Credential.ApiKey); r.Headers.Add("anthropic-version", "2023-06-01"); return r; }
    protected override IReadOnlyList<ProviderModel> ParseModels(JsonElement root) => root.GetProperty("data").EnumerateArray().Select(x => { var id=x.GetProperty("id").GetString()!; return new ProviderModel(id,x.TryGetProperty("display_name",out var n)?n.GetString()??id:id,null,Lifecycle(id),ModelCapability.TextGeneration|ModelCapability.Reasoning|ModelCapability.StructuredOutput|ModelCapability.ToolUse,CapabilityMetadataSource.VersionedProviderMapping,null,null); }).ToList();
    protected override HttpRequestMessage CompletionRequest(ProviderConnectionContext c, RoutedModel m, LanguageModelRequest q) { var r = new HttpRequestMessage(HttpMethod.Post,"v1/messages"); r.Headers.Add("x-api-key",c.Credential.ApiKey);r.Headers.Add("anthropic-version","2023-06-01");r.Content=JsonContent.Create(new{model=m.ProviderModelId,max_tokens=q.MaximumOutputTokens,temperature=0,system=$"{q.SystemInstructions}\nOutput JSON Schema:\n{q.JsonSchema}",messages=new[]{new{role="user",content=q.UserContent}}});return r; }
    protected override LanguageModelResponse ParseCompletion(JsonElement root,HttpResponseMessage response) { var usage=root.GetProperty("usage");return new(root.GetProperty("content")[0].GetProperty("text").GetString()!,root.GetProperty("id").GetString(),usage.TryGetProperty("input_tokens",out var i)?i.GetInt32():null,usage.TryGetProperty("output_tokens",out var o)?o.GetInt32():null); }
}

internal class OpenAiProviderAdapter(HttpClient http,IOptions<ExecutionOptions>? options=null,ProviderCapacityCoordinator? coordinator=null,TimeProvider? clock=null) : ProviderAdapterBase(http,options,coordinator,clock)
{
    public override ProviderType ProviderType => ProviderType.OpenAI;
    protected override HttpRequestMessage ModelsRequest(ProviderConnectionContext c) => Bearer(HttpMethod.Get,"v1/models",c.Credential.ApiKey);
    protected override IReadOnlyList<ProviderModel> ParseModels(JsonElement root) => root.GetProperty("data").EnumerateArray().Where(x=>IsLanguage(x.GetProperty("id").GetString()!)).Select(x=>{var id=x.GetProperty("id").GetString()!;var lifecycle=id.StartsWith("gpt-3.5",StringComparison.OrdinalIgnoreCase)?ModelLifecycleStatus.Deprecated:Lifecycle(id);var capabilities=ModelCapability.TextGeneration;if(IsReviewed(id)){capabilities|=ModelCapability.Reasoning|ModelCapability.StructuredOutput|ModelCapability.ToolUse;if(id.Contains("mini",StringComparison.OrdinalIgnoreCase)||id.Contains("nano",StringComparison.OrdinalIgnoreCase))capabilities|=ModelCapability.LowCost|ModelCapability.FastResponse;}return new ProviderModel(id,id,null,lifecycle,capabilities,IsReviewed(id)?CapabilityMetadataSource.VersionedProviderMapping:CapabilityMetadataSource.ConservativeDefault,null,null);}).ToList();
    private static bool IsLanguage(string id)=>id.StartsWith("gpt",StringComparison.OrdinalIgnoreCase)||id.StartsWith("o",StringComparison.OrdinalIgnoreCase);
    protected override HttpRequestMessage CompletionRequest(ProviderConnectionContext c,RoutedModel m,LanguageModelRequest q){var responses=ProviderType==ProviderType.OpenAI&&UsesResponses(m.ProviderModelId);var r=Bearer(HttpMethod.Post,responses?"v1/responses":"v1/chat/completions",c.Credential.ApiKey);using var schema=JsonDocument.Parse(q.JsonSchema);if(responses){r.Content=JsonContent.Create(new{model=m.ProviderModelId,instructions=q.SystemInstructions,input=q.UserContent,max_output_tokens=q.MaximumOutputTokens,text=new{format=new{type="json_schema",name="impersonate_result",strict=true,schema=schema.RootElement.Clone()}}});return r;}var body=new Dictionary<string,object?>{{"model",m.ProviderModelId},{"messages",new[]{new{role="system",content=q.SystemInstructions},new{role="user",content=q.UserContent}}},{"max_tokens",q.MaximumOutputTokens},{"temperature",0}};if(SupportsJsonMode(m.ProviderModelId))body["response_format"]=new{type="json_schema",json_schema=new{name="impersonate_result",strict=true,schema=schema.RootElement.Clone()}};r.Content=JsonContent.Create(body);return r;}
    private static bool IsReviewed(string id)=>System.Text.RegularExpressions.Regex.IsMatch(id,@"^(gpt-(4\.1|5(?:\.\d+)?)(-(mini|nano|pro|codex))?)(-\d{4}-\d{2}-\d{2})?$",System.Text.RegularExpressions.RegexOptions.IgnoreCase)||System.Text.RegularExpressions.Regex.IsMatch(id,@"^o[34]",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static bool UsesResponses(string id)=>id.StartsWith("gpt-5",StringComparison.OrdinalIgnoreCase)||id.StartsWith("o3",StringComparison.OrdinalIgnoreCase)||id.StartsWith("o4",StringComparison.OrdinalIgnoreCase);
    private static bool SupportsJsonMode(string id)=>id.StartsWith("gpt-4o",StringComparison.OrdinalIgnoreCase)||id.StartsWith("gpt-4.1",StringComparison.OrdinalIgnoreCase)||id.StartsWith("gpt-5",StringComparison.OrdinalIgnoreCase)||id.StartsWith("o",StringComparison.OrdinalIgnoreCase);
    protected override LanguageModelResponse ParseCompletion(JsonElement root,HttpResponseMessage response){var usage=root.TryGetProperty("usage",out var u)?u:default;if(root.TryGetProperty("output",out var output)){var content=output.EnumerateArray().SelectMany(x=>x.TryGetProperty("content",out var c)?c.EnumerateArray():[]).FirstOrDefault(x=>x.TryGetProperty("text",out _));return new(content.ValueKind==JsonValueKind.Object?content.GetProperty("text").GetString()!:string.Empty,root.TryGetProperty("id",out var rid)?rid.GetString():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("input_tokens",out var ri)?ri.GetInt32():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("output_tokens",out var ro)?ro.GetInt32():null);}return new(root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!,root.TryGetProperty("id",out var id)?id.GetString():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("prompt_tokens",out var i)?i.GetInt32():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("completion_tokens",out var o)?o.GetInt32():null);}
}

internal sealed class OpenRouterProviderAdapter(HttpClient http,IOptions<ExecutionOptions>? options=null,ProviderCapacityCoordinator? coordinator=null,TimeProvider? clock=null) : OpenAiProviderAdapter(http,options,coordinator,clock)
{
    public override ProviderType ProviderType => ProviderType.OpenRouter;
}

internal sealed class GeminiProviderAdapter(HttpClient http,IOptions<ExecutionOptions>? options=null,ProviderCapacityCoordinator? coordinator=null,TimeProvider? clock=null) : ProviderAdapterBase(http,options,coordinator,clock)
{
    public override ProviderType ProviderType => ProviderType.GoogleGemini;
    protected override HttpRequestMessage ModelsRequest(ProviderConnectionContext c)=>new(HttpMethod.Get,$"v1beta/models?pageSize=1000&key={Uri.EscapeDataString(c.Credential.ApiKey)}");
    protected override IReadOnlyList<ProviderModel> ParseModels(JsonElement root)=>root.GetProperty("models").EnumerateArray().Where(x=>x.GetProperty("supportedGenerationMethods").EnumerateArray().Any(y=>y.GetString()=="generateContent")).Select(x=>{var id=x.GetProperty("name").GetString()!.Replace("models/","");return new ProviderModel(id,x.TryGetProperty("displayName",out var n)?n.GetString()??id:id,x.TryGetProperty("description",out var d)?d.GetString():null,Lifecycle(id),ModelCapability.TextGeneration|ModelCapability.Reasoning|ModelCapability.StructuredOutput|ModelCapability.ToolUse,CapabilityMetadataSource.LiveProviderMetadata,x.TryGetProperty("inputTokenLimit",out var i)?i.GetInt32():null,x.TryGetProperty("outputTokenLimit",out var o)?o.GetInt32():null);}).ToList();
    protected override HttpRequestMessage CompletionRequest(ProviderConnectionContext c,RoutedModel m,LanguageModelRequest q){var r=new HttpRequestMessage(HttpMethod.Post,$"v1beta/models/{Uri.EscapeDataString(m.ProviderModelId)}:generateContent?key={Uri.EscapeDataString(c.Credential.ApiKey)}");r.Content=JsonContent.Create(new{system_instruction=new{parts=new[]{new{text=$"{q.SystemInstructions}\nOutput JSON Schema:\n{q.JsonSchema}"}}},contents=new[]{new{parts=new[]{new{text=q.UserContent}}}},generationConfig=new{responseMimeType="application/json",maxOutputTokens=q.MaximumOutputTokens,temperature=0}});return r;}
    protected override LanguageModelResponse ParseCompletion(JsonElement root,HttpResponseMessage response){var usage=root.TryGetProperty("usageMetadata",out var u)?u:default;return new(root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()!,null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("promptTokenCount",out var i)?i.GetInt32():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("candidatesTokenCount",out var o)?o.GetInt32():null);}
}
