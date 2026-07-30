using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record StoredArtifact(string Reference, string Sha256, long ContentLength, string MediaType, DateTimeOffset CreatedAtUtc);
