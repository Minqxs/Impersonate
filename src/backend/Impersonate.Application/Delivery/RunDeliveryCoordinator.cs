using Impersonate.Application.Pipelines;
using Impersonate.Application.Projects;
using Impersonate.Domain.Delivery;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Delivery;

internal sealed class RunDeliveryCoordinator(IPipelineRunRepository runs, IProjectRepository projects, IRunDeliveryRepository deliveries) : IRunDeliveryCoordinator
{
    public async Task<DeliveryOperationResult<RunDelivery>> GetOrCreateAsync(Guid projectId, Guid runId, CancellationToken ct)
    {
        var existing = await deliveries.GetByRunAsync(projectId, runId, ct);
        if (existing is not null)
            return DeliveryOperationResult<RunDelivery>.Ok(existing);
        var project = await projects.GetAsync(projectId, ct);
        var run = await runs.GetAsync(projectId, runId, ct);
        if (project is null || run is null)
            return DeliveryOperationResult<RunDelivery>.Fail("run_delivery_not_found", "Project or pipeline run was not found.");
        if (run.Status != PipelineRunStatus.ReadyForDelivery)
            return DeliveryOperationResult<RunDelivery>.Fail("run_delivery_not_ready", "Pipeline run is not ready for delivery.");
        var approved = run.Tasks.Where(x => x.Status == PlannedTaskStatus.Approved).SelectMany(x => x.Attempts).Where(x => !string.IsNullOrWhiteSpace(x.SourceBaseCommitSha)).ToArray();
        if (approved.Length == 0)
            return DeliveryOperationResult<RunDelivery>.Fail("run_delivery_base_missing", "Approved delivery source base is unavailable.");
        var sourceBase = approved[0].SourceBaseCommitSha!;
        if (approved.Any(x => !string.Equals(x.SourceBaseCommitSha, sourceBase, StringComparison.OrdinalIgnoreCase)))
            return DeliveryOperationResult<RunDelivery>.Fail("run_delivery_base_conflict", "Approved tasks do not share one source base.");
        var created = RunDelivery.Create(projectId, runId, project.DefaultBranch, sourceBase, RunBranchNameGenerator.Create(runId, run.FeatureRequest));
        await deliveries.AddAsync(created, ct);
        try
        {
            await deliveries.SaveChangesAsync(ct);
        }
        catch { existing = await deliveries.GetByRunAsync(projectId, runId, ct); if (existing is not null) return DeliveryOperationResult<RunDelivery>.Ok(existing); throw; }
        return DeliveryOperationResult<RunDelivery>.Ok(created);
    }
}
