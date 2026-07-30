using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public interface IExecutionArtifactStore
{
    Task<StoredArtifact> WriteTextAsync(ArtifactScope scope, string name, string content, string mediaType, CancellationToken ct);
    Task<string> ReadTextAsync(string reference, int maximumCharacters, CancellationToken ct);
}
