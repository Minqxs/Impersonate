using Impersonate.Application.Pipelines;
using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

internal sealed class FinalPullRequestOrchestrator(IRunDeliveryRepository deliveries, IPipelineRunRepository runs, IFinalPullRequestGateway gateway) : IFinalPullRequestOrchestrator
{
    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = await deliveries.ClaimNextFinalPullRequestAsync(Guid.NewGuid(), workerId, now, now.AddMinutes(5), ct);
        if (delivery is null)
            return false;
        try
        {
            if (delivery.Status == RunDeliveryStatus.ReadyForFinalPullRequest)
            {
                var run = await runs.GetAsync(delivery.ProjectId, delivery.PipelineRunId, ct);
                if (run is null)
                {
                    delivery.Block("final_pull_request_context_missing", "Pipeline run was not found.", now);
                    return true;
                }
                var result = await gateway.OpenAsync(delivery, run.FeatureRequest, $"Pipeline run: `{run.Id}`\n\nThis pull request contains the aggregate of {run.Tasks.Count} independently reviewed and integrated task deliveries.\n\nReviewed head: `{delivery.FinalReviewedHeadSha}`", ct);
                if (!result.Succeeded)
                {
                    delivery.ReleaseClaim();
                    return true;
                }
                var pull = result.Value!;
                delivery.RecordFinalPullRequest(pull.Provider, pull.Repository, pull.Number, pull.Url, pull.HeadSha, pull.BaseBranch, now);
            }
            if (delivery.Status == RunDeliveryStatus.FinalPullRequestOpen)
            {
                var result = await gateway.ReadAsync(delivery, ct);
                if (!result.Succeeded)
                {
                    delivery.ReleaseClaim();
                    return true;
                }
                var observation = result.Value!;
                if (observation.Merged)
                {
                    delivery.Block("final_pull_request_merged_without_user_action", "The final pull request merged outside the explicit Merge to main action.", now);
                    return true;
                }
                if (!observation.Open)
                {
                    delivery.Block("final_pull_request_closed", "The final pull request was closed without merge.", now);
                    return true;
                }
                if (observation.MergeableState == "mergeable" && observation.ChecksState == "passed")
                    delivery.RecordMainReadiness(observation.MergeableState, observation.ChecksState, now);
            }
            delivery.ReleaseClaim();
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { delivery.ReleaseClaim(); throw; }
        finally { await deliveries.SaveChangesAsync(CancellationToken.None); }
    }
}
