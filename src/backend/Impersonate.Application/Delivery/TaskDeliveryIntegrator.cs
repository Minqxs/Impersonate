using Impersonate.Application.Pipelines;
using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

internal sealed class TaskDeliveryIntegrator(ITaskDeliveryRepository deliveries, ITaskDeliveryReviewRepository reviews, IRunDeliveryRepository runDeliveries, IPullRequestGateway pullRequests) : ITaskDeliveryIntegrator
{
    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = await deliveries.ClaimNextIntegrationAsync(Guid.NewGuid(), workerId, now, now.AddMinutes(5), ct);
        if (delivery is null)
            return false;
        try
        {
            var current = (await reviews.ListAsync(delivery.Id, ct)).SingleOrDefault(x => x.IsCurrent);
            if (current?.Decision != DeliveryReviewDecision.Approved || !string.Equals(current.ExactHeadSha, delivery.CommitSha, StringComparison.OrdinalIgnoreCase))
            {
                delivery.Block("delivery_integration_approval_missing", "Automatic integration requires a current exact-head approval.", now);
                return true;
            }
            if (delivery.Status == TaskDeliveryStatus.ApprovedForIntegration)
                delivery.RequestMerge(now);
            delivery.ReleaseClaim();
            await deliveries.SaveChangesAsync(ct);
            var result = await pullRequests.MergeAsync(delivery, ct);
            if (!result.Succeeded)
            {
                delivery.ReleaseClaim();
                return true;
            }
            var observation = result.Value!;
            if (observation.State != PullRequestExternalState.Merged)
            {
                if (observation.HasConflicts)
                    delivery.BeginConflictResolution(now);
                delivery.ReleaseClaim();
                return true;
            }
            if (string.IsNullOrWhiteSpace(observation.MergeCommitSha))
            {
                delivery.ReleaseClaim();
                return true;
            }
            delivery.MarkMergedIntoRun(now);
            var aggregate = await runDeliveries.GetByRunAsync(delivery.ProjectId, delivery.PipelineRunId, ct) ?? throw new InvalidOperationException("Run delivery was not found.");
            aggregate.RecordIntegratedHead(observation.MergeCommitSha);
            delivery.ReleaseClaim();
            await runDeliveries.SaveChangesAsync(ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { delivery.ReleaseClaim(); throw; }
        finally { await deliveries.SaveChangesAsync(CancellationToken.None); }
    }
}
