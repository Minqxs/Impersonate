using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed class ExecutionOptions
{
    public string? WorkspaceRoot
    {
        get; set;
    }
    public string? ArtifactRoot
    {
        get; set;
    }
    public string? DeliveryRoot
    {
        get; set;
    }
    public string DeliveryCommitName { get; set; } = "Impersonate";
    public string DeliveryCommitEmail { get; set; } = "impersonate@localhost";
    public int MaximumArtifactBytes { get; set; } = 2_000_000;
    public int MaximumToolOutputCharacters { get; set; } = 100_000;
    public int MaximumCoderProviderRounds { get; set; } = 30;
    public int MaximumCoderToolExecutions { get; set; } = 150;
    public int DefaultCoderMaximumOutputTokens { get; set; } = 16_000;
    public int DefaultReviewerMaximumOutputTokens { get; set; } = 16_000;
    public int DefaultModelContextWindowTokens { get; set; } = 128_000;
    public int MaximumStructuredOutputRepairAttempts { get; set; } = 1;
    public int MaximumModelFallbacks { get; set; } = 2;
    public int MaximumSameModelRateLimitRetries { get; set; } = 2;
    public int MaximumAutomaticRateLimitWaitSeconds { get; set; } = 15;
    public int MaximumTotalRateLimitWaitSecondsPerOperation { get; set; } = 30;
    public int InitialRateLimitBackoffMilliseconds { get; set; } = 1000;
    public int MaximumRateLimitBackoffSeconds { get; set; } = 8;
    public int RateLimitJitterMaximumMilliseconds { get; set; } = 250;
    public int CommandTimeoutSeconds { get; set; } = 120;
    public int ClaimMinutes { get; set; } = 15;
    public int MaximumWorkspacePreparationAttempts { get; set; } = 3;
}
