using Impersonate.Application.Ai;

namespace Impersonate.Infrastructure.Agents.Execution;

internal static class AdaptiveOutputReservationPolicy
{
    public static OutputReservationDecision Reserve(OutputReservationContext context)
    {
        var endpointFloor = context.Endpoint == ProviderEndpoint.Responses ? 1_200 : 900;
        var (requested, purpose) = context.Phase switch
        {
            "Implementation" => (Math.Max(3_000, context.EstimatedDiffTokens * 2 + 1_000), "implementation turn sized from the expected diff"),
            "Validation" => (1_600, "validation/result turn"),
            "Completion" => (1_200, "completion turn"),
            _ when context.PatchExists => (1_800, "post-patch inspection turn"),
            _ => (endpointFloor, "small native discovery turn")
        };
        if (context.PendingToolPayloadTokens > 0)
        {
            requested += Math.Min(2_000, context.PendingToolPayloadTokens / 2);
            purpose += $"; accounts for {context.PendingToolPayloadTokens} tokens of native tool results";
        }
        if (context.PreviousOutputTokens >= requested * 3 / 4)
        {
            requested = Math.Max(requested, context.PreviousOutputTokens.Value + 800);
            purpose += "; raised from prior observed output usage";
        }

        if (context.PriorOutputTruncated)
        {
            requested = Math.Max(requested, Math.Max(endpointFloor, context.PreviousReservation.GetValueOrDefault(endpointFloor)) * 2);
            purpose += "; safely increased after provider truncation";
        }

        if (context.ProviderResetObserved)
        {
            if (context.Phase is not "Implementation")
                requested = Math.Max(endpointFloor, requested * 3 / 4);
            purpose += $"; provider reset metadata reduced non-patch pressure ({context.LastRateLimitScope?.ToString() ?? "unknown"} scope)";
        }
        if (context.ProviderRemainingTokens is > 0 && context.Phase is not "Implementation")
        {
            requested = (int)Math.Min(requested, Math.Max(endpointFloor, context.ProviderRemainingTokens.Value / 4));
            purpose += "; bounded using reported remaining token capacity";
        }

        var bounded = Math.Clamp(requested, 1, Math.Max(1, context.ModelMaximumOutputTokens));
        if (bounded < requested)
            purpose += "; capped at the model's supported output";
        return new(bounded, purpose);
    }
}
