using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record CoderResult(bool Succeeded, string Summary, IReadOnlyList<string> ChangedFiles, IReadOnlyList<string> ValidationNotes, int ToolStepCount, string? ProviderRequestId, int? InputTokenCount, int? OutputTokenCount, string? FailureCode = null, string? FailureMessage = null, string? ResponseType = null, int SuccessfulReadCount = 0, int SuccessfulSearchCount = 0, int SuccessfulPatchCount = 0, bool RepositoryInspected = false, bool CurrentDiffExists = false, int PrematureCompletionCount = 0, int ProviderRoundTripCount = 0, int ConsecutiveReadOnlyRounds = 0, int MaximumSingleRequestInput = 0, string? ProviderResponseStatus = null, string? ProviderIncompleteReason = null, int StructuredOutputRepairCount = 0, int NoProgressCorrectionCount = 0, int PaidProviderRequestCount = 0, string CurrentPhase = "Discovery", string? RequestedProhibitedTool = null, int PatchAttemptCount = 0, int FailedPatchCount = 0, string? LastPatchFailureCode = null);
