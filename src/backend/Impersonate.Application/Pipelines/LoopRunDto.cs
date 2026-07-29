using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record LoopRunDto(Guid Id, string LoopDefinitionId, string LoopDefinitionVersion, LoopRunStatus Status, LoopStage CurrentStage, int MaximumRevisionAttempts, bool ContinueOnTaskFailure, int RetryCount, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, string? StopReason, string? FailureReason);
