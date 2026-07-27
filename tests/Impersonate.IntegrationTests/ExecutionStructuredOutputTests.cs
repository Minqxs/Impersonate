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
}
