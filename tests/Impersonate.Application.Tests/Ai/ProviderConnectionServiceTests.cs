using Impersonate.Application;
using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Impersonate.Application.Tests.Ai;

public sealed class ProviderConnectionServiceTests
{
    [Fact]
    public async Task Create_stores_connection_and_credential_with_one_commit()
    {
        var repository = new FakeRepository();
        var credentials = new FakeCredentialStore();
        var service = CreateService(repository, credentials);

        var result = await service.CreateAsync(ProviderType.OpenAI, new("OpenAI", " secret "), default);

        Assert.Equal(1, repository.SaveCount);
        Assert.Single(repository.Connections);
        Assert.Equal("secret", credentials.Values[result.Id].ApiKey);
    }

    [Fact]
    public async Task Create_does_not_commit_connection_when_credential_storage_fails()
    {
        var repository = new FakeRepository();
        var credentials = new FakeCredentialStore { FailStore = true };
        var service = CreateService(repository, credentials);

        await Assert.ThrowsAsync<ProviderCredentialStorageException>(() =>
            service.CreateAsync(ProviderType.OpenAI, new("OpenAI", "secret"), default));

        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(repository.CommittedConnections);
    }

    [Fact]
    public async Task Replace_upserts_secret_preserves_identity_and_resets_failure()
    {
        var connection = AiProviderConnection.Create(ProviderType.OpenAI, "OpenAI");
        connection.ValidationFailed(true, "invalid_key", "The key was rejected.");
        var repository = new FakeRepository(connection);
        var credentials = new FakeCredentialStore();
        var service = CreateService(repository, credentials);

        var result = await service.ReplaceCredentialsAsync(connection.Id, new("new-secret"), default);

        Assert.NotNull(result);
        Assert.Equal(connection.Id, result.Id);
        Assert.Equal(ProviderConnectionStatus.PendingValidation, result.Status);
        Assert.Null(result.LastFailureCode);
        Assert.Equal("new-secret", credentials.Values[connection.Id].ApiKey);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Create_rejects_duplicate_provider_connection()
    {
        var repository = new FakeRepository(AiProviderConnection.Create(ProviderType.OpenAI, "Existing"));
        var credentials = new FakeCredentialStore();
        var service = CreateService(repository, credentials);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ProviderType.OpenAI, new("Duplicate", "secret"), default));

        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(credentials.Values);
    }

    private static IAiProviderConnectionService CreateService(FakeRepository repository, FakeCredentialStore credentials) =>
        new ServiceCollection().AddApplication()
            .AddSingleton<IAiRoutingRepository>(repository)
            .AddSingleton<IProviderCredentialStore>(credentials)
            .AddSingleton<IAiProviderAdapter>(new FakeAdapter())
            .BuildServiceProvider().GetRequiredService<IAiProviderConnectionService>();

    private sealed class FakeRepository(params AiProviderConnection[] connections) : IAiRoutingRepository
    {
        public List<AiProviderConnection> Connections { get; } = [.. connections];
        public IReadOnlyList<AiProviderConnection> CommittedConnections { get; private set; } = [.. connections];
        public int SaveCount
        {
            get; private set;
        }
        public Task<IReadOnlyList<AiProviderConnection>> GetConnectionsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AiProviderConnection>>(Connections);
        public Task<AiProviderConnection?> GetConnectionAsync(Guid id, CancellationToken ct) => Task.FromResult(Connections.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DiscoveredModel>> GetModelsAsync(Guid? id, CancellationToken ct) => Task.FromResult<IReadOnlyList<DiscoveredModel>>([]);
        public Task<ProjectAiRoutingPolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => Task.FromResult<ProjectAiRoutingPolicy?>(null);
        public Task<ModelSelectionDecision?> GetDecisionAsync(Guid project, Guid run, CancellationToken ct) => Task.FromResult<ModelSelectionDecision?>(null);
        public Task AddConnectionAsync(AiProviderConnection connection, CancellationToken ct)
        {
            Connections.Add(connection);
            return Task.CompletedTask;
        }
        public Task AddModelAsync(DiscoveredModel model, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveConnectionAsync(AiProviderConnection connection, CancellationToken ct) => Task.CompletedTask;
        public Task<ProjectAiRoutingPolicy> GetOrCreatePolicyAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task AddDecisionAsync(ModelSelectionDecision decision, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            CommittedConnections = [.. Connections];
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialStore : IProviderCredentialStore
    {
        public Dictionary<Guid, ProviderCredential> Values { get; } = [];
        public bool FailStore
        {
            get; init;
        }
        public Task StoreAsync(Guid id, ProviderCredential credential, CancellationToken ct)
        {
            if (FailStore)
                throw new ProviderCredentialStorageException();
            Values[id] = credential;
            return Task.CompletedTask;
        }
        public Task<ProviderCredentialReadResult> RetrieveAsync(Guid id, CancellationToken ct) => Task.FromResult(
            Values.TryGetValue(id, out var value)
                ? new ProviderCredentialReadResult(ProviderCredentialReadStatus.Found, value, null, null)
                : new ProviderCredentialReadResult(ProviderCredentialReadStatus.Missing, null, "credentials_missing", "Credential is missing."));
        public Task DeleteAsync(Guid id, CancellationToken ct)
        {
            Values.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAdapter : IAiProviderAdapter
    {
        public ProviderType ProviderType => ProviderType.OpenAI;
        public Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext connection, CancellationToken ct) => Task.FromResult(new ProviderValidationResult(true, false, null, "Connected."));
        public Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext connection, CancellationToken ct) => Task.FromResult<IReadOnlyList<ProviderModel>>([]);
        public Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext connection, RoutedModel model, LanguageModelRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
