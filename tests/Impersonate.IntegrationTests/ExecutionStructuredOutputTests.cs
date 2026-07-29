using System.Net;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Ai;
using Impersonate.Infrastructure.Agents.Execution;
using Microsoft.Extensions.Options;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class ExecutionStructuredOutputTests
{
    [Fact]
    public void OpenAi_execution_schemas_satisfy_strict_object_rules()
    {
        AssertStrictObjects(CoderAgent.StructuredOutputSchema);
        AssertStrictObjects(ReviewerAgent.StructuredOutputSchema);
    }

    [Fact]
    public async Task Coder_preserves_safe_provider_rejection_details()
    {
        var agent = new CoderAgent([new RejectingAdapter()], new CredentialStore(), new UnusedTools(), Options.Create(new ExecutionOptions()));
        var model = new SelectedModel(Guid.NewGuid(), Guid.NewGuid(), ProviderType.OpenAI, "gpt-4.1", ModelSelectionSource.AutomaticRouting, 100, "test");
        var result = await agent.ExecuteAsync(new(Guid.NewGuid(), Guid.NewGuid(), "Feature", Guid.NewGuid(), "Task", "Description", ["Done"], 1, 0, null, [], new("workspace"), model), default);
        Assert.False(result.Succeeded);
        Assert.Equal("provider_request_rejected", result.FailureCode);
        Assert.Contains("HTTP 400", result.FailureMessage);
        Assert.Contains("schema", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coder_rejects_oversized_input_before_calling_provider()
    {
        var adapter = new CountingAdapter();
        var agent = new CoderAgent([adapter], new CredentialStore(), new UnusedTools(), Options.Create(new ExecutionOptions { MaximumModelInputTokens = 1000 }));
        var model = new SelectedModel(Guid.NewGuid(), Guid.NewGuid(), ProviderType.OpenAI, "gpt-4.1", ModelSelectionSource.AutomaticRouting, 100, "test");
        var result = await agent.ExecuteAsync(new(Guid.NewGuid(), Guid.NewGuid(), new string('x', 10_000), Guid.NewGuid(), "Task", "Description", ["Done"], 1, 0, null, [], new("workspace"), model), default);
        Assert.False(result.Succeeded);
        Assert.Equal("request_token_budget_exceeded", result.FailureCode);
        Assert.Equal(0, adapter.CallCount);
    }

    [Fact]
    public async Task Coder_rejects_two_premature_completions_with_preloaded_evidence()
    {
        var adapter=new ImmediateCompleteAdapter();
        var tools=new EvidenceTools();
        var agent=new CoderAgent([adapter],new CredentialStore(),tools,Options.Create(new ExecutionOptions{MaximumCoderSteps=5,MaximumModelInputTokens=4000}));
        var model=new SelectedModel(Guid.NewGuid(),Guid.NewGuid(),ProviderType.OpenAI,"gpt-4.1",ModelSelectionSource.AutomaticRouting,100,"test");
        var result=await agent.ExecuteAsync(new(Guid.NewGuid(),Guid.NewGuid(),"Feature",Guid.NewGuid(),"Add IsActive","Add property",["Property exists"],1,0,null,[],new("workspace"),model,RepositoryEvidence:["backend/src/User.cs"]),default);
        Assert.False(result.Succeeded);
        Assert.Equal("coder_protocol_failed",result.FailureCode);
        Assert.Equal(2,result.PrematureCompletionCount);
        Assert.True(result.RepositoryInspected);
        Assert.Equal(1,result.SuccessfulReadCount);
        Assert.Equal(2,adapter.Requests.Count);
        Assert.Contains("public Guid Id",adapter.Requests[0].UserContent);
        Assert.Contains("completion_rejected",adapter.Requests[1].UserContent);
    }

    [Fact]
    public async Task Coder_stops_repeated_read_only_rounds_before_tool_budget()
    {
        var adapter=new RepeatedReadAdapter();
        var tools=new EvidenceTools();
        var agent=new CoderAgent([adapter],new CredentialStore(),tools,Options.Create(new ExecutionOptions
        {
            MaximumCoderSteps=20,
            MaximumCoderProviderRounds=6,
            MaximumConsecutiveReadOnlyRounds=3,
            MaximumCoderRoundsBeforePatch=4,
            MaximumModelInputTokens=4000
        }));
        var model=new SelectedModel(Guid.NewGuid(),Guid.NewGuid(),ProviderType.OpenAI,"gpt-4.1",ModelSelectionSource.AutomaticRouting,100,"test");

        var result=await agent.ExecuteAsync(new(Guid.NewGuid(),Guid.NewGuid(),"Feature",Guid.NewGuid(),"Add DisplayName","Add one property",["Property exists"],1,0,null,[],new("workspace"),model,RepositoryEvidence:["backend/src/User.cs"]),default);

        Assert.False(result.Succeeded);
        Assert.Equal("coder_no_patch_progress",result.FailureCode);
        Assert.True(result.ProviderRoundTripCount<=5);
        Assert.True(result.ToolStepCount<20);
        Assert.Equal(0,result.SuccessfulPatchCount);
        Assert.Equal(1,result.NoProgressCorrectionCount);
        Assert.Contains("mandatory_implementation",adapter.Requests[^1].UserContent);
        Assert.True(adapter.Requests.Sum(x=>x.UserContent.Length)<25_000);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData(".env")]
    [InlineData("C:/absolute.cs")]
    public async Task Coder_rejects_unsafe_evidence_before_provider_call(string path)
    {
        var adapter=new ImmediateCompleteAdapter();
        var agent=new CoderAgent([adapter],new CredentialStore(),new EvidenceTools(),Options.Create(new ExecutionOptions()));
        var model=new SelectedModel(Guid.NewGuid(),Guid.NewGuid(),ProviderType.OpenAI,"gpt-4.1",ModelSelectionSource.AutomaticRouting,100,"test");
        var result=await agent.ExecuteAsync(new(Guid.NewGuid(),Guid.NewGuid(),"Feature",Guid.NewGuid(),"Task","Description",["Done"],1,0,null,[],new("workspace"),model,RepositoryEvidence:[path]),default);
        Assert.False(result.Succeeded);
        Assert.Equal("coder_evidence_rejected",result.FailureCode);
        Assert.Empty(adapter.Requests);
    }

    private static void AssertStrictObjects(string schema)
    {
        using var document = JsonDocument.Parse(schema);
        Visit(document.RootElement);
        static void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var isObject = element.TryGetProperty("type", out var type) && (type.ValueKind == JsonValueKind.String && type.GetString() == "object" || type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(x => x.GetString() == "object"));
                if (isObject)
                {
                    Assert.True(element.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False);
                    if (element.TryGetProperty("properties", out var properties))
                    {
                        var names = properties.EnumerateObject().Select(x => x.Name).Order().ToArray();
                        var required = element.GetProperty("required").EnumerateArray().Select(x => x.GetString()!).Order().ToArray();
                        Assert.Equal(names, required);
                    }
                }
                foreach (var property in element.EnumerateObject()) Visit(property.Value);
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Visit(item);
        }
    }

    private sealed class RejectingAdapter : IAiProviderAdapter
    {
        public ProviderType ProviderType => ProviderType.OpenAI;
        public Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext connection, RoutedModel model, LanguageModelRequest request, CancellationToken cancellationToken) => throw new ProviderRequestException("provider_request_rejected", "The provider rejected the request. HTTP 400: Invalid response schema.", HttpStatusCode.BadRequest, false);
        public Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext connection, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext connection, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class CountingAdapter : IAiProviderAdapter
    {
        public int CallCount { get; private set; }
        public ProviderType ProviderType => ProviderType.OpenAI;
        public Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext connection, RoutedModel model, LanguageModelRequest request, CancellationToken cancellationToken) { CallCount++; throw new NotSupportedException(); }
        public Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext connection, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext connection, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class ImmediateCompleteAdapter:IAiProviderAdapter
    {
        public List<LanguageModelRequest> Requests{get;}=[];
        public ProviderType ProviderType=>ProviderType.OpenAI;
        public Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext connection,RoutedModel model,LanguageModelRequest request,CancellationToken cancellationToken){Requests.Add(request);return Task.FromResult(new LanguageModelResponse("{\"type\":\"complete\",\"calls\":null,\"summary\":\"done\",\"validationNotes\":[],\"knownLimitations\":[]}","request",10,5));}
        public Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext connection,CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext connection,CancellationToken cancellationToken)=>throw new NotSupportedException();
    }
    private sealed class RepeatedReadAdapter:IAiProviderAdapter
    {
        public List<LanguageModelRequest> Requests{get;}=[];
        public ProviderType ProviderType=>ProviderType.OpenAI;
        public Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext connection,RoutedModel model,LanguageModelRequest request,CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var body="{\"type\":\"tool_calls\",\"calls\":[{\"id\":\"read\",\"tool\":\"read_file\",\"arguments\":{\"path\":\"backend/src/User.cs\",\"query\":null,\"patch\":null,\"executable\":null,\"arguments\":null,\"workingDirectory\":null,\"timeoutSeconds\":null}}],\"summary\":null,\"validationNotes\":null,\"knownLimitations\":null}";
            return Task.FromResult(new LanguageModelResponse(body,"request",100,20));
        }
        public Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext connection,CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext connection,CancellationToken cancellationToken)=>throw new NotSupportedException();
    }
    private sealed class CredentialStore : IProviderCredentialStore
    {
        public Task<ProviderCredentialReadResult> RetrieveAsync(Guid connectionId, CancellationToken cancellationToken) => Task.FromResult(new ProviderCredentialReadResult(ProviderCredentialReadStatus.Found, new("test-key"), null, null));
        public Task StoreAsync(Guid connectionId, ProviderCredential credential, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Guid connectionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class UnusedTools : IRepositoryTools
    {
        private static Task<RepositoryToolResult> Unused() => throw new NotSupportedException();
        public Task<RepositoryToolResult> ListFilesAsync(WorkspaceReference workspace, string relativePath, CancellationToken ct) => Unused();
        public Task<RepositoryToolResult> ReadFileAsync(WorkspaceReference workspace, string relativePath, CancellationToken ct) => Unused();
        public Task<RepositoryToolResult> SearchTextAsync(WorkspaceReference workspace, string query, string relativePath, CancellationToken ct) => Unused();
        public Task<RepositoryToolResult> ApplyPatchAsync(WorkspaceReference workspace, string patch, CancellationToken ct) => Unused();
        public Task<RepositoryToolResult> GetDiffAsync(WorkspaceReference workspace, CancellationToken ct) => Unused();
        public Task<RepositoryToolResult> RunCommandAsync(WorkspaceReference workspace, RepositoryCommand command, CancellationToken ct) => Unused();
    }
    private sealed class EvidenceTools:IRepositoryTools
    {
        public Task<RepositoryToolResult> ReadFileAsync(WorkspaceReference workspace,string relativePath,CancellationToken ct)=>Task.FromResult(new RepositoryToolResult(true,"public class User { public Guid Id { get; set; } }"));
        public Task<RepositoryToolResult> GetDiffAsync(WorkspaceReference workspace,CancellationToken ct)=>Task.FromResult(new RepositoryToolResult(true,string.Empty));
        public Task<RepositoryToolResult> ListFilesAsync(WorkspaceReference workspace,string relativePath,CancellationToken ct)=>throw new NotSupportedException();
        public Task<RepositoryToolResult> SearchTextAsync(WorkspaceReference workspace,string query,string relativePath,CancellationToken ct)=>throw new NotSupportedException();
        public Task<RepositoryToolResult> ApplyPatchAsync(WorkspaceReference workspace,string patch,CancellationToken ct)=>throw new NotSupportedException();
        public Task<RepositoryToolResult> RunCommandAsync(WorkspaceReference workspace,RepositoryCommand command,CancellationToken ct)=>throw new NotSupportedException();
    }
}
