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
        Assert.Equal("coder_mandatory_implementation_protocol_failed",result.FailureCode);
        Assert.True(result.ProviderRoundTripCount<=5);
        Assert.True(result.ToolStepCount<20);
        Assert.Equal(0,result.SuccessfulPatchCount);
        Assert.Equal(1,result.NoProgressCorrectionCount);
        Assert.Contains("mandatory_implementation",adapter.Requests[^1].UserContent);
        Assert.True(adapter.Requests.Sum(x=>x.UserContent.Length)<25_000);
    }

    [Fact]
    public async Task Coder_preserves_discovered_profile_sources_for_mandatory_implementation()
    {
        using var repository=TemporaryProfileRepository.Create();var adapter=new WorkingSetAdapter();var tools=new ProfileTools(repository.Root);
        var agent=new CoderAgent([adapter],new CredentialStore(),tools,Options.Create(new ExecutionOptions{MaximumCoderSteps=20,MaximumCoderProviderRounds=6,MaximumConsecutiveReadOnlyRounds=3,MaximumCoderRoundsBeforePatch=4,MaximumImplementationWorkingSetCharacters=12000,MaximumModelInputTokens=8000}));
        var model=new SelectedModel(Guid.NewGuid(),Guid.NewGuid(),ProviderType.OpenAI,"gpt-4.1",ModelSelectionSource.AutomaticRouting,100,"test");

        var result=await agent.ExecuteAsync(new(Guid.NewGuid(),Guid.NewGuid(),"Add DisplayName",Guid.NewGuid(),"Add DisplayName","Expose a read-only DisplayName derived from profiles",["DisplayName uses profile FullName","Focused tests pass"],1,0,null,[],new("workspace"),model,RepositoryEvidence:["backend/src/HomeTaskSA.Domain/Entities/User.cs"]),default);

        Assert.True(result.Succeeded,result.FailureMessage);Assert.Equal(1,tools.PatchCalls);Assert.True(tools.DiffCalls>0);Assert.Contains("backend/src/HomeTaskSA.Domain/Entities/User.cs",result.ChangedFiles);Assert.Equal("Completion",result.CurrentPhase);
        var correction=adapter.Requests.First(x=>x.UserContent.Contains("mandatory_implementation"));Assert.Contains("CustomerProfile",correction.UserContent);Assert.Contains("ServiceProviderProfile",correction.UserContent);Assert.Contains("FullName",correction.UserContent);Assert.DoesNotContain("tool_results",correction.UserContent);Assert.True(correction.UserContent.Length<12000);
    }

    [Fact]
    public async Task Coder_rejects_discovery_tool_after_mandatory_correction_without_executing_it()
    {
        var adapter=new ProhibitedReadAdapter();var tools=new ProfileTools();var agent=new CoderAgent([adapter],new CredentialStore(),tools,Options.Create(new ExecutionOptions{MaximumConsecutiveReadOnlyRounds=1,MaximumCoderRoundsBeforePatch=1,MaximumCoderProviderRounds=4}));var model=new SelectedModel(Guid.NewGuid(),Guid.NewGuid(),ProviderType.OpenAI,"gpt-4.1",ModelSelectionSource.AutomaticRouting,100,"test");
        var result=await agent.ExecuteAsync(new(Guid.NewGuid(),Guid.NewGuid(),"Feature",Guid.NewGuid(),"Task","Description",["Done"],1,0,null,[],new("workspace"),model,RepositoryEvidence:["backend/src/HomeTaskSA.Domain/Entities/User.cs"]),default);
        Assert.False(result.Succeeded);Assert.Equal("coder_mandatory_implementation_protocol_failed",result.FailureCode);Assert.Equal("read_file",result.RequestedProhibitedTool);Assert.Equal(2,tools.ReadCalls);
    }

    [Fact]
    public async Task Coder_maps_valid_blocker_without_fallback_code()
    {
        var agent=new CoderAgent([new BlockedAdapter()],new CredentialStore(),new EvidenceTools(),Options.Create(new ExecutionOptions()));var model=new SelectedModel(Guid.NewGuid(),Guid.NewGuid(),ProviderType.OpenAI,"gpt-4.1",ModelSelectionSource.AutomaticRouting,100,"test");var result=await agent.ExecuteAsync(new(Guid.NewGuid(),Guid.NewGuid(),"Feature",Guid.NewGuid(),"Task","Description",["Done"],1,0,null,[],new("workspace"),model),default);Assert.False(result.Succeeded);Assert.Equal("coder_missing_repository_evidence",result.FailureCode);Assert.Contains("profile",result.FailureMessage!,StringComparison.OrdinalIgnoreCase);
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
    private sealed class WorkingSetAdapter:IAiProviderAdapter
    {
        public List<LanguageModelRequest> Requests{get;}=[];public ProviderType ProviderType=>ProviderType.OpenAI;
        public Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext c,RoutedModel m,LanguageModelRequest q,CancellationToken ct){Requests.Add(q);var n=Requests.Count;var body=n switch{1=>Calls("search_text","{\"path\":\"backend/src\",\"query\":\"FullName\",\"patch\":null,\"executable\":null,\"arguments\":null,\"workingDirectory\":null,\"timeoutSeconds\":null}"),2=>"{\"type\":\"tool_calls\",\"calls\":[{\"id\":\"c\",\"tool\":\"read_file\",\"arguments\":{\"path\":\"backend/src/CustomerProfile.cs\",\"query\":null,\"patch\":null,\"executable\":null,\"arguments\":null,\"workingDirectory\":null,\"timeoutSeconds\":null}},{\"id\":\"s\",\"tool\":\"read_file\",\"arguments\":{\"path\":\"backend/src/ServiceProviderProfile.cs\",\"query\":null,\"patch\":null,\"executable\":null,\"arguments\":null,\"workingDirectory\":null,\"timeoutSeconds\":null}}],\"summary\":null,\"validationNotes\":null,\"knownLimitations\":null,\"blockerCode\":null,\"blockerMessage\":null,\"missingEvidencePaths\":null}",3=>Calls("search_text","{\"path\":\"backend/tests\",\"query\":\"User\",\"patch\":null,\"executable\":null,\"arguments\":null,\"workingDirectory\":null,\"timeoutSeconds\":null}"),4=>Calls("apply_patch","{\"path\":null,\"query\":null,\"patch\":\"*** Begin Patch\\n*** Update File: backend/src/HomeTaskSA.Domain/Entities/User.cs\\n@@\\n+ public string DisplayName => CustomerProfile?.FullName ?? ServiceProviderProfile?.FullName ?? string.Empty;\\n*** End Patch\",\"executable\":null,\"arguments\":null,\"workingDirectory\":null,\"timeoutSeconds\":null}"),5=>Calls("get_diff","{\"path\":null,\"query\":null,\"patch\":null,\"executable\":null,\"arguments\":null,\"workingDirectory\":null,\"timeoutSeconds\":null}"),_=>"{\"type\":\"complete\",\"calls\":null,\"summary\":\"Added DisplayName\",\"validationNotes\":[\"diff verified\"],\"knownLimitations\":[],\"blockerCode\":null,\"blockerMessage\":null,\"missingEvidencePaths\":null}"};return Task.FromResult(new LanguageModelResponse(body,"request",100,20));}
        private static string Calls(string tool,string args)=>$"{{\"type\":\"tool_calls\",\"calls\":[{{\"id\":\"x\",\"tool\":\"{tool}\",\"arguments\":{args}}}],\"summary\":null,\"validationNotes\":null,\"knownLimitations\":null,\"blockerCode\":null,\"blockerMessage\":null,\"missingEvidencePaths\":null}}";
        public Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext c,CancellationToken ct)=>throw new NotSupportedException();public Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext c,CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class ProhibitedReadAdapter:IAiProviderAdapter
    {
        private int calls;public ProviderType ProviderType=>ProviderType.OpenAI;public Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext c,RoutedModel m,LanguageModelRequest q,CancellationToken ct){calls++;var body="{\"type\":\"tool_calls\",\"calls\":[{\"id\":\"r\",\"tool\":\"read_file\",\"arguments\":{\"path\":\"backend/src/CustomerProfile.cs\",\"query\":null,\"patch\":null,\"executable\":null,\"arguments\":null,\"workingDirectory\":null,\"timeoutSeconds\":null}}],\"summary\":null,\"validationNotes\":null,\"knownLimitations\":null,\"blockerCode\":null,\"blockerMessage\":null,\"missingEvidencePaths\":null}";return Task.FromResult(new LanguageModelResponse(body,$"request-{calls}",10,5));}public Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext c,CancellationToken ct)=>throw new NotSupportedException();public Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext c,CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class BlockedAdapter:IAiProviderAdapter
    {
        public ProviderType ProviderType=>ProviderType.OpenAI;public Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext c,RoutedModel m,LanguageModelRequest q,CancellationToken ct)=>Task.FromResult(new LanguageModelResponse("{\"type\":\"blocked\",\"calls\":null,\"summary\":null,\"validationNotes\":[],\"knownLimitations\":[],\"blockerCode\":\"missing_repository_evidence\",\"blockerMessage\":\"The required profile contract cannot be located.\",\"missingEvidencePaths\":[\"Profile.cs\"]}","request",10,5));public Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext c,CancellationToken ct)=>throw new NotSupportedException();public Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext c,CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class ProfileTools(string? root=null):IRepositoryTools
    {
        public int PatchCalls{get;private set;}public int DiffCalls{get;private set;}public int ReadCalls{get;private set;}
        public Task<RepositoryToolResult> ReadFileAsync(WorkspaceReference w,string p,CancellationToken ct){ReadCalls++;var candidate=root is null?null:Path.Combine(root,p.Replace('/',Path.DirectorySeparatorChar));var text=candidate is not null&&File.Exists(candidate)?File.ReadAllText(candidate):p.EndsWith("User.cs")?"class User { public CustomerProfile? CustomerProfile {get;set;} public ServiceProviderProfile? ServiceProviderProfile {get;set;} }":p.Contains("Customer")?"class CustomerProfile { public string FullName {get;set;} = string.Empty; }":"class ServiceProviderProfile { public string FullName {get;set;} = string.Empty; }";return Task.FromResult(new RepositoryToolResult(true,text));}
        public Task<RepositoryToolResult> SearchTextAsync(WorkspaceReference w,string q,string p,CancellationToken ct)=>Task.FromResult(new RepositoryToolResult(true,q=="FullName"?"backend/src/CustomerProfile.cs: FullName\nbackend/src/ServiceProviderProfile.cs: FullName":"backend/tests/UserTests.cs"));
        public Task<RepositoryToolResult> ApplyPatchAsync(WorkspaceReference w,string p,CancellationToken ct){PatchCalls++;if(root is not null)File.AppendAllText(Path.Combine(root,"backend","src","HomeTaskSA.Domain","Entities","User.cs"),"\npublic string DisplayName => CustomerProfile?.FullName ?? ServiceProviderProfile?.FullName ?? string.Empty;\n");return Task.FromResult(new RepositoryToolResult(true,"patch applied"));}
        public Task<RepositoryToolResult> GetDiffAsync(WorkspaceReference w,CancellationToken ct){DiffCalls++;return Task.FromResult(new RepositoryToolResult(true,PatchCalls>0?"diff --git a/backend/src/HomeTaskSA.Domain/Entities/User.cs b/backend/src/HomeTaskSA.Domain/Entities/User.cs\n+DisplayName":""));}
        public Task<RepositoryToolResult> RunCommandAsync(WorkspaceReference w,RepositoryCommand c,CancellationToken ct)=>Task.FromResult(new RepositoryToolResult(true,"backend/src/HomeTaskSA.Domain/Entities/User.cs"));public Task<RepositoryToolResult> ListFilesAsync(WorkspaceReference w,string p,CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class TemporaryProfileRepository(string root):IDisposable
    {
        public string Root=>root;
        public static TemporaryProfileRepository Create(){var root=Path.Combine(Path.GetTempPath(),"impersonate-coder-"+Guid.NewGuid().ToString("N"));var entities=Path.Combine(root,"backend","src","HomeTaskSA.Domain","Entities");var tests=Path.Combine(root,"backend","tests","HomeTaskSA.Domain.Tests");Directory.CreateDirectory(entities);Directory.CreateDirectory(tests);File.WriteAllText(Path.Combine(entities,"User.cs"),"class User { public CustomerProfile? CustomerProfile {get;set;} public ServiceProviderProfile? ServiceProviderProfile {get;set;} }");File.WriteAllText(Path.Combine(root,"backend","src","CustomerProfile.cs"),"class CustomerProfile { public string FullName {get;set;} = string.Empty; }");File.WriteAllText(Path.Combine(root,"backend","src","ServiceProviderProfile.cs"),"class ServiceProviderProfile { public string FullName {get;set;} = string.Empty; }");File.WriteAllText(Path.Combine(tests,"UserTests.cs"),"class UserTests { }");return new(root);}
        public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
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
