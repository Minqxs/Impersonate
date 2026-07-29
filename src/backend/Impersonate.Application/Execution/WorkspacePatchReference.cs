using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record WorkspacePatchReference(Guid TaskId, int Sequence, string ArtifactReference);
