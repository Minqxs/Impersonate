namespace Impersonate.Domain.Pipelines;

public sealed class ExecutionInvocation
{
    private ExecutionInvocation()
    {
    }

    public Guid Id
    {
        get; private set;
    }
    public Guid TaskAttemptId
    {
        get; private set;
    }
    public int Sequence
    {
        get; private set;
    }
    public string AgentRole { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public Guid? SelectionDecisionId
    {
        get; private set;
    }
    public string PromptVersion { get; private set; } = string.Empty;
    public string? ProviderRequestId
    {
        get; private set;
    }
    public int? InputTokenCount
    {
        get; private set;
    }
    public int? OutputTokenCount
    {
        get; private set;
    }
    public string? ResponseType
    {
        get; private set;
    }
    public int ToolStepCount
    {
        get; private set;
    }
    public int SuccessfulReadCount
    {
        get; private set;
    }
    public int SuccessfulSearchCount
    {
        get; private set;
    }
    public int SuccessfulPatchCount
    {
        get; private set;
    }
    public int PatchAttemptCount
    {
        get; private set;
    }
    public int FailedPatchCount
    {
        get; private set;
    }
    public string? LastPatchFailureCode
    {
        get; private set;
    }
    public int MaximumRequestedOutputReservation { get; private set; }
    public string OutputReservationReasonsJson { get; private set; } = "[]";
    public long ProviderCapacityWaitMilliseconds { get; private set; }
    public bool ProviderResetUsed { get; private set; }
    public string? LastRateLimitScope { get; private set; }
    public int ProviderRoundTripCount
    {
        get; private set;
    }
    public int ConsecutiveReadOnlyRounds
    {
        get; private set;
    }
    public int MaximumSingleRequestInput
    {
        get; private set;
    }
    public string? ProviderResponseStatus
    {
        get; private set;
    }
    public string? ProviderIncompleteReason
    {
        get; private set;
    }
    public int StructuredOutputRepairCount
    {
        get; private set;
    }
    public int NoProgressCorrectionCount
    {
        get; private set;
    }
    public int PaidProviderRequestCount
    {
        get; private set;
    }
    public string CurrentPhase { get; private set; } = "Discovery";
    public string? RequestedProhibitedTool
    {
        get; private set;
    }
    public int FallbackSequence
    {
        get; private set;
    }
    public ExecutionInvocationStatus Status
    {
        get; private set;
    }
    public string? FailureCode
    {
        get; private set;
    }
    public string? FailureReason
    {
        get; private set;
    }
    public DateTimeOffset StartedAtUtc
    {
        get; private set;
    }
    public DateTimeOffset CompletedAtUtc
    {
        get; private set;
    }

    public static ExecutionInvocation Record(Guid taskAttemptId, int sequence, string role, string provider, string model, Guid? selectionDecisionId, string promptVersion, string? requestId, int? input, int? output, string? responseType, int toolSteps, int reads, int searches, int patches, int fallbackSequence, bool succeeded, string? failureCode, string? failureReason, DateTimeOffset startedAt, DateTimeOffset completedAt, int providerRounds = 0, int readOnlyRounds = 0, int maximumSingleRequestInput = 0, string? providerStatus = null, string? incompleteReason = null, int repairs = 0, int corrections = 0, int paidRequests = 0, string phase = "Discovery", string? prohibitedTool = null, int patchAttempts = 0, int failedPatches = 0, string? lastPatchFailureCode = null, int maximumRequestedOutputReservation = 0, string outputReservationReasonsJson = "[]", long providerCapacityWaitMilliseconds = 0, bool providerResetUsed = false, string? lastRateLimitScope = null) => new()
    {
        Id = Guid.NewGuid(),
        TaskAttemptId = taskAttemptId,
        Sequence = sequence,
        AgentRole = role,
        Provider = provider,
        Model = model,
        SelectionDecisionId = selectionDecisionId,
        PromptVersion = promptVersion,
        ProviderRequestId = requestId,
        InputTokenCount = input,
        OutputTokenCount = output,
        ResponseType = responseType,
        ToolStepCount = toolSteps,
        SuccessfulReadCount = reads,
        SuccessfulSearchCount = searches,
        SuccessfulPatchCount = patches,
        PatchAttemptCount = patchAttempts,
        FailedPatchCount = failedPatches,
        LastPatchFailureCode = lastPatchFailureCode,
        MaximumRequestedOutputReservation = maximumRequestedOutputReservation,
        OutputReservationReasonsJson = outputReservationReasonsJson,
        ProviderCapacityWaitMilliseconds = providerCapacityWaitMilliseconds,
        ProviderResetUsed = providerResetUsed,
        LastRateLimitScope = lastRateLimitScope,
        ProviderRoundTripCount = providerRounds,
        ConsecutiveReadOnlyRounds = readOnlyRounds,
        MaximumSingleRequestInput = maximumSingleRequestInput,
        ProviderResponseStatus = providerStatus,
        ProviderIncompleteReason = incompleteReason,
        StructuredOutputRepairCount = repairs,
        NoProgressCorrectionCount = corrections,
        PaidProviderRequestCount = paidRequests,
        CurrentPhase = phase,
        RequestedProhibitedTool = prohibitedTool,
        FallbackSequence = fallbackSequence,
        Status = succeeded ? ExecutionInvocationStatus.Succeeded : ExecutionInvocationStatus.Failed,
        FailureCode = failureCode,
        FailureReason = failureReason,
        StartedAtUtc = startedAt,
        CompletedAtUtc = completedAt
    };
}
