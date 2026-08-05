using Impersonate.Application.Pipelines;
using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

internal sealed class TaskDeliveryReconciler(ITaskDeliveryRepository deliveries, IRunDeliveryRepository runDeliveries, IPullRequestGateway pullRequests) : ITaskDeliveryReconciler
{
    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = await deliveries.ClaimNextReconciliationAsync(Guid.NewGuid(), workerId, now, now.AddMinutes(5), ct);
        if (delivery is null)
            return false;
        try
        {
            var result = await pullRequests.ReadAsync(delivery, ct);
            if (!result.Succeeded)
            {
                if (result.Code is "github_mcp_unavailable" or "github_mcp_timeout" or "github_mcp_authentication_unavailable" or "github_mcp_failed")
                    delivery.ReleaseClaim();
                else
                    delivery.Block(result.Code ?? "delivery_reconciliation_failed", result.Error ?? "Pull-request reconciliation failed safely.");
                return true;
            }
            var observation = result.Value!;
            if (observation.State == PullRequestExternalState.Open)
                delivery.ReleaseClaim();
            else if (observation.State == PullRequestExternalState.Closed)
                delivery.Block("delivery_pull_request_closed", "The task pull request was closed without merge.");
            else
            {
                if (delivery.Status != TaskDeliveryStatus.MergeRequested)
                {
                    delivery.Block("delivery_merged_without_approval", "The task pull request merged without an exact-head delivery approval.");
                    return true;
                }
                if (string.IsNullOrWhiteSpace(observation.MergeCommitSha))
                {
                    delivery.ReleaseClaim();
                    return true;
                }
                delivery.MarkMergedIntoRun();
                delivery.ReleaseClaim();
                var aggregate = await runDeliveries.GetByRunAsync(delivery.ProjectId, delivery.PipelineRunId, ct) ?? throw new InvalidOperationException("Run delivery was not found.");
                aggregate.RecordIntegratedHead(observation.MergeCommitSha);
                await runDeliveries.SaveChangesAsync(ct);
            }
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { delivery.ReleaseClaim(); throw; }
        finally { await deliveries.SaveChangesAsync(CancellationToken.None); }
    }
}
