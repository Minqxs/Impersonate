using Impersonate.Application.Execution;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Projects;
using Impersonate.Domain.Pipelines;
using Microsoft.Extensions.Options;

namespace Impersonate.Application.Delivery;

internal sealed class TaskDeliveryOrchestrator(ITaskDeliveryRepository deliveries, ITaskDeliveryCoordinator coordinator, ITargetRepositoryDeliveryService target, ITaskDeliveryPushService push, IProjectRepository projects, IPipelineRunRepository runs, IOptions<ExecutionOptions> options) : ITaskDeliveryOrchestrator
{
    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken ct)
    {
        await MaterializeEligibleDeliveriesAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var delivery = await deliveries.ClaimNextPendingAsync(Guid.NewGuid(), workerId, now, now.AddMinutes(options.Value.ClaimMinutes), ct);
        if (delivery is null) return false;
        try
        {
            var handoff = await coordinator.BuildHandoffAsync(delivery.ProjectId, delivery.PipelineRunId, delivery.PlannedTaskId, ct);
            if (!handoff.Succeeded) { delivery.Block(handoff.Code ?? "delivery_handoff_invalid", handoff.Error ?? "Delivery handoff is invalid."); delivery.ReleaseClaim(); }
            else
            {
                var result = await target.DeliverApprovedPatchAsync(delivery, handoff.Value!, ct);
                if (!result.Succeeded) { delivery.Block(result.Code ?? "delivery_failed", result.Error ?? "Local delivery preparation failed safely."); delivery.ReleaseClaim(); }
                else
                {
                    var pushed = await push.PushAsync(delivery, ct);
                    if (!pushed.Succeeded) delivery.Block(pushed.Code ?? "delivery_push_failed", pushed.Error ?? "Task branch could not be pushed safely.");
                    delivery.ReleaseClaim();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { delivery.ReleaseClaim(); throw; }
        catch (Exception ex) { delivery.Block(SafeCode(ex), "Local task delivery could not complete. Review the bounded delivery diagnostics before recovery."); delivery.ReleaseClaim(); }
        finally { await deliveries.SaveChangesAsync(CancellationToken.None); }
        return true;
    }

    private async Task MaterializeEligibleDeliveriesAsync(CancellationToken ct)
    {
        foreach (var project in await projects.ListAsync(null, null, ct))
        foreach (var run in await runs.ListAsync(project.Id, PipelineRunStatus.ReadyForDelivery, null, null, ct))
        foreach (var item in await coordinator.GetEligibilityAsync(project.Id, run.Id, ct))
            if (item.Eligible) await coordinator.GetOrCreateAsync(project.Id, run.Id, item.PlannedTaskId, ct);
    }
    private static string SafeCode(Exception ex)
    {
        var code = ex.Message.Split(':', 2)[0];
        return code.StartsWith("delivery_", StringComparison.Ordinal) ? code[..Math.Min(100, code.Length)] : "delivery_failed";
    }
}
