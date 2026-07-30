using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record PriorModelFailure(string Code, ProviderType Provider, string Model, int? CodingStrength = null, int? RepositoryToolReliability = null, int? StructuredOutputReliability = null, int? ContextTier = null, Guid? DecisionId = null);
