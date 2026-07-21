using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Impersonate.Application.Planning;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Agents.Planner;

internal sealed class AnthropicLanguageModelClient(HttpClient http):ILanguageModelClient
{
 public async Task<Impersonate.Application.Planning.LanguageModelResponse> CompleteAsync(Impersonate.Application.Planning.LanguageModelRequest request,CancellationToken ct)
 {
  using var message=new HttpRequestMessage(HttpMethod.Post,"v1/messages");
  message.Headers.Add("anthropic-version","2023-06-01");
  message.Content=JsonContent.Create(new {model=request.Model,max_tokens=request.MaximumOutputTokens,temperature=0,system=$"{request.SystemInstructions}\nOutput JSON Schema:\n{request.JsonSchema}",messages=new[]{new{role="user",content=request.UserContent}}});
  using var response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,ct);
  var json=await response.Content.ReadAsStringAsync(ct);
  if(!response.IsSuccessStatusCode)throw new HttpRequestException($"Anthropic request failed with status {(int)response.StatusCode}.");
  using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
  var content=root.GetProperty("content")[0].GetProperty("text").GetString()??throw new InvalidDataException("Anthropic returned empty content.");
  var usage=root.TryGetProperty("usage",out var u)?u:default;
  return new(content,root.TryGetProperty("id",out var id)?id.GetString():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("input_tokens",out var i)?i.GetInt32():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("output_tokens",out var o)?o.GetInt32():null);
 }
}

internal sealed class PlannerAgent(ILanguageModelClient client,IOptions<PlannerOptions> options,IEnumerable<Impersonate.Application.Ai.IAiProviderAdapter> adapters,Impersonate.Application.Ai.IProviderCredentialStore credentials):IPlannerAgent
{
 private static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
 public async Task<PlannerAgentResult> PlanAsync(PlannerAgentRequest request,CancellationToken ct)
 {
  var prompt=await LoadPromptAsync(request.PromptVersion,ct);
  var context=JsonSerializer.Serialize(new{project=new{request.ProjectName,request.ProjectDescription,request.DefaultBranch},request.FeatureRequest,constraints=new{request.MaximumTasks,repositoryInspectionAvailable=false},request.CorrectionContext});
  var schema="""{"type":"object","additionalProperties":false,"required":["summary","canPlan","planningNotes","tasks","failureReason","clarifyingQuestion"],"properties":{"summary":{"type":"string"},"canPlan":{"type":"boolean"},"planningNotes":{"type":"array","items":{"type":"string"}},"tasks":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["sequence","title","description","acceptanceCriteria"],"properties":{"sequence":{"type":"integer","minimum":1},"title":{"type":"string"},"description":{"type":"string"},"acceptanceCriteria":{"type":"array","items":{"type":"string"},"minItems":1}}}},"failureReason":{"type":["string","null"]},"clarifyingQuestion":{"type":["string","null"]}}}""";
  Impersonate.Application.Planning.LanguageModelResponse response;
  if(request.ProviderConnectionId is{} connectionId&&request.RoutedProvider is{} provider&&!string.IsNullOrWhiteSpace(request.RoutedModel))
  {
   var credentialRead=await credentials.RetrieveAsync(connectionId,ct);if(credentialRead.Status!=Impersonate.Application.Ai.ProviderCredentialReadStatus.Found)throw new Impersonate.Application.Ai.ProviderCredentialUnavailableException(credentialRead.SafeFailureCode!,credentialRead.SafeFailureMessage!);var credential=credentialRead.Credential!;
   var adapter=adapters.Single(x=>x.ProviderType==provider);
   var routed=await adapter.CompleteAsync(new(connectionId,provider,credential),new(null,request.RoutedModel),new(request.RoutedModel,prompt,context,schema,options.Value.MaximumOutputTokens),ct);
   response=new(routed.Content,routed.ProviderRequestId,routed.InputTokenCount,routed.OutputTokenCount);
  }
  else response=await client.CompleteAsync(new(options.Value.Model,prompt,context,schema,options.Value.MaximumOutputTokens),ct);
  var plan=JsonSerializer.Deserialize<PlannerPlan>(response.Content,Json)??throw new InvalidDataException("Planner returned an empty response.");
  return new(plan,response.ProviderRequestId,response.InputTokenCount,response.OutputTokenCount);
 }
 private static async Task<string> LoadPromptAsync(string version,CancellationToken ct){if(version!="planner-v1")throw new InvalidOperationException("Unsupported planner prompt version.");var assembly=typeof(PlannerAgent).Assembly;var name=assembly.GetManifestResourceNames().Single(x=>x.EndsWith($"Prompts.{version}.md",StringComparison.Ordinal));await using var stream=assembly.GetManifestResourceStream(name)!;using var reader=new StreamReader(stream);return await reader.ReadToEndAsync(ct);}
}
