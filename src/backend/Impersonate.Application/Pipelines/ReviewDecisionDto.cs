using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record ReviewDecisionDto(Guid Id, Guid TaskAttemptId, ReviewDecisionType Decision, string? Provider, string? Model, string? PromptVersion, int? InputTokenCount, int? OutputTokenCount, string? ReviewedPatchSha256, string Summary, string? Feedback, string FindingsJson, bool IsCurrent, DateTimeOffset CreatedAtUtc);
