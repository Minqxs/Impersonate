using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record ExecutionInvocationDto(Guid Id, int Sequence, string AgentRole, string Provider, string Model, string PromptVersion, string? ProviderRequestId, int? InputTokenCount, int? OutputTokenCount, string? ResponseType, int ToolStepCount, int SuccessfulReadCount, int SuccessfulSearchCount, int SuccessfulPatchCount, int FallbackSequence, ExecutionInvocationStatus Status, string? FailureCode, string? FailureReason, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc, int ProviderRoundTripCount = 0, int ConsecutiveReadOnlyRounds = 0, int MaximumSingleRequestInput = 0, string? ProviderResponseStatus = null, string? ProviderIncompleteReason = null, int StructuredOutputRepairCount = 0, int NoProgressCorrectionCount = 0, int PaidProviderRequestCount = 0, string CurrentPhase = "Discovery", string? RequestedProhibitedTool = null, int PatchAttemptCount = 0, int FailedPatchCount = 0, string? LastPatchFailureCode = null, int MaximumRequestedOutputReservation = 0, IReadOnlyList<string>? OutputReservationReasons = null, long ProviderCapacityWaitMilliseconds = 0, bool ProviderResetUsed = false, string? LastRateLimitScope = null);
