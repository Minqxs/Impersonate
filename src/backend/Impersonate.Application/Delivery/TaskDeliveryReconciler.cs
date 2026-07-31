using Impersonate.Application.Pipelines;
using Impersonate.Domain.Delivery;

namespace Impersonate.Application.Delivery;

internal sealed class TaskDeliveryReconciler(ITaskDeliveryRepository deliveries, IPipelineRunRepository runs, IPullRequestGateway pullRequests) : ITaskDeliveryReconciler
{
    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = await deliveries.ClaimNextReconciliationAsync(Guid.NewGuid(), workerId, now, now.AddMinutes(5), ct);
        if (delivery is null) return false;
        try
        {
            var result = await pullRequests.ReadAsync(delivery, ct);
            if (!result.Succeeded)
            {
                if (result.Code is "github_mcp_unavailable" or "github_mcp_timeout" or "github_mcp_authentication_unavailable" or "github_mcp_failed") delivery.ReleaseClaim();
                else delivery.Block(result.Code ?? "delivery_reconciliation_failed", result.Error ?? "Pull-request reconciliation failed safely.");
                return true;
            }
            var observation = result.Value!;
            if (observation.State == PullRequestExternalState.Open) delivery.ReleaseClaim();
            else if (observation.State == PullRequestExternalState.Closed) delivery.Block("delivery_pull_request_closed", "The task pull request was closed without merge.");
            else
            {
                delivery.MarkMerged();
                delivery.ReleaseClaim();
                var run = await runs.GetAsync(delivery.ProjectId, delivery.PipelineRunId, ct) ?? throw new InvalidOperationException("Delivery run was not found.");
                if (run.Tasks.Where(x => x.Status == Domain.Pipelines.PlannedTaskStatus.Approved).All(task => run.Deliveries.SingleOrDefault(x => x.PlannedTaskId == task.Id)?.Status == TaskDeliveryStatus.Merged)
                    && run.Deliveries.All(x => x.Status == TaskDeliveryStatus.Merged))
                    run.CompleteDelivery();
            }
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { delivery.ReleaseClaim(); throw; }
        finally { await deliveries.SaveChangesAsync(CancellationToken.None); }
    }
}
