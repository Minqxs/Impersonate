using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record PipelineEventDto(Guid Id, Guid? PlannedTaskId, string EventType, string? PreviousState, string NewState, string Message, DateTimeOffset CreatedAtUtc, int Sequence);
