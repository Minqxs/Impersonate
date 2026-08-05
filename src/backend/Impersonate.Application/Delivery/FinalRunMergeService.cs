using Impersonate.Application.Pipelines;
using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

internal sealed class FinalRunMergeService(IRunDeliveryRepository deliveries, IPipelineRunRepository runs, IFinalPullRequestGateway gateway) : IFinalRunMergeService
{
    public async Task<DeliveryOperationResult<FinalRunMergeReference>> MergeAsync(RunDelivery delivery, CancellationToken ct)
    {
        if (delivery.Status is not (RunDeliveryStatus.ReadyForMain or RunDeliveryStatus.MergeRequested) || delivery.RequiredChecksState != "passed" || delivery.FinalPullRequestMergeableState != "mergeable")
            return DeliveryOperationResult<FinalRunMergeReference>.Fail("final_merge_not_ready", "Final pull request is not ready to merge to main.");
        if (delivery.Status == RunDeliveryStatus.ReadyForMain)
        {
            delivery.RequestMerge();
            await deliveries.SaveChangesAsync(ct);
        }
        var result = await gateway.MergeAsync(delivery, ct);
        if (!result.Succeeded)
            return result;
        delivery.MarkMerged();
        var run = await runs.GetAsync(delivery.ProjectId, delivery.PipelineRunId, ct) ?? throw new InvalidOperationException("Pipeline run was not found.");
        run.CompleteDelivery();
        await deliveries.SaveChangesAsync(ct);
        await runs.SaveChangesAsync(ct);
        return result;
    }
}
