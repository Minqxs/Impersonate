using Impersonate.Application.Ai;
using Impersonate.Infrastructure.Agents.Execution;
using Impersonate.Domain.Pipelines;
using System.Text.Json;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class AdaptiveOutputReservationTests
{
    [Fact]
    public void Discovery_does_not_reserve_the_old_sixteen_thousand_default()
    {
        var result = AdaptiveOutputReservationPolicy.Reserve(new(ProviderEndpoint.Responses, 16_000, 2_000, "Discovery", false));
        Assert.Equal(1_200, result.Tokens);
        Assert.Contains("discovery", result.Reason);
    }

    [Fact]
    public void Implementation_reservation_scales_with_expected_diff()
    {
        var small = AdaptiveOutputReservationPolicy.Reserve(new(ProviderEndpoint.Responses, 16_000, 1_000, "Implementation", false));
        var large = AdaptiveOutputReservationPolicy.Reserve(new(ProviderEndpoint.Responses, 16_000, 5_000, "Implementation", false));
        Assert.True(small.Tokens >= 3_000);
        Assert.True(large.Tokens > small.Tokens);
    }

    [Fact]
    public void Truncation_raises_next_reservation_without_exceeding_model_support()
    {
        var result = AdaptiveOutputReservationPolicy.Reserve(new(ProviderEndpoint.Responses, 5_000, 1_000, "Discovery", false, PreviousReservation: 3_000, PriorOutputTruncated: true));
        Assert.Equal(5_000, result.Tokens);
        Assert.Contains("truncation", result.Reason);
        Assert.Contains("supported output", result.Reason);
    }

    [Fact]
    public void Reservations_are_per_turn_and_have_no_cumulative_task_budget()
    {
        var first = AdaptiveOutputReservationPolicy.Reserve(new(ProviderEndpoint.Responses, 16_000, 2_000, "Discovery", false));
        var later = AdaptiveOutputReservationPolicy.Reserve(new(ProviderEndpoint.Responses, 16_000, 2_000, "Implementation", false, PreviousReservation: first.Tokens));
        Assert.True(later.Tokens > first.Tokens);
    }

    [Fact]
    public void Tool_payload_and_provider_capacity_change_non_patch_reservations()
    {
        var baseline = AdaptiveOutputReservationPolicy.Reserve(new(ProviderEndpoint.Responses, 16_000, 2_000, "Validation", true));
        var payload = AdaptiveOutputReservationPolicy.Reserve(new(ProviderEndpoint.Responses, 16_000, 2_000, "Validation", true, PendingToolPayloadTokens: 2_000));
        var constrained = AdaptiveOutputReservationPolicy.Reserve(new(ProviderEndpoint.Responses, 16_000, 2_000, "Validation", true, PendingToolPayloadTokens: 2_000, ProviderResetObserved: true, LastRateLimitScope: RateLimitScope.Tokens, ProviderRemainingTokens: 5_000));
        Assert.True(payload.Tokens > baseline.Tokens);
        Assert.True(constrained.Tokens < payload.Tokens);
        Assert.Contains("remaining token capacity", constrained.Reason);
    }

    [Fact]
    public void Invocation_record_persists_reservation_capacity_and_actual_usage()
    {
        var reasons = JsonSerializer.Serialize(new[] { "Round 1: discovery" });
        var invocation = ExecutionInvocation.Record(Guid.NewGuid(), 1, "Coder", "OpenAI", "gpt-5", null, "coder-v1", "request", 100, 25, "complete_task", 3, 1, 0, 1, 0, true, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, maximumRequestedOutputReservation: 3_000, outputReservationReasonsJson: reasons, providerCapacityWaitMilliseconds: 250, providerResetUsed: true, lastRateLimitScope: "Tokens");
        Assert.Equal(25, invocation.OutputTokenCount);
        Assert.Equal(3_000, invocation.MaximumRequestedOutputReservation);
        Assert.Equal(reasons, invocation.OutputReservationReasonsJson);
        Assert.Equal(250, invocation.ProviderCapacityWaitMilliseconds);
        Assert.True(invocation.ProviderResetUsed);
        Assert.Equal("Tokens", invocation.LastRateLimitScope);
    }
}
