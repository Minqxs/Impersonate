using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record ReviewerResult(bool Succeeded, ReviewDecisionType? Decision, string Summary, string? Feedback, IReadOnlyList<ReviewFinding> Findings, string? ProviderRequestId, int? InputTokenCount, int? OutputTokenCount, string? FailureCode = null, string? FailureMessage = null);
