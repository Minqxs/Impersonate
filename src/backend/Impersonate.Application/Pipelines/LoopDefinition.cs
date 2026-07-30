using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record LoopDefinition(string Id, string Name, string Version, IReadOnlyList<LoopStage> Stages, int MaximumRevisionAttempts, bool ContinueOnTaskFailure);
