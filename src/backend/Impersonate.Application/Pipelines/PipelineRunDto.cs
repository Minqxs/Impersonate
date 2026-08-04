using Impersonate.Application.Ai;
using Impersonate.Application.Delivery;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record PipelineRunDto(Guid Id, Guid ProjectId, string FeatureRequest, PipelineRunStatus Status, DateTimeOffset CreatedAtUtc, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, DateTimeOffset? CancelledAtUtc, string? FailureReason, string? StopReason, LoopRunDto Loop, IReadOnlyList<PlannedTaskDto> Tasks, IReadOnlyList<PlanningAttemptDto> PlanningAttempts, string? InfrastructureFailureCode = null, string? InfrastructureFailureMessage = null, Guid? InfrastructureBlockedTaskId = null, IReadOnlyList<string>? PlanningWarnings = null, RunDeliveryDto? RunDelivery = null);
