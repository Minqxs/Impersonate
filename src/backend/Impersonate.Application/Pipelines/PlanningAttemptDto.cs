using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record PlanningAttemptDto(int AttemptNumber, string Provider, string Model, string PromptVersion, PlanningAttemptStatus Status, DateTimeOffset StartedAtUtc, DateTimeOffset? CompletedAtUtc, string? FailureCode, string? FailureMessage, int? InputTokenCount, int? OutputTokenCount);
