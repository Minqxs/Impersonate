using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Delivery;
using Impersonate.Application.Execution;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Projects;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Delivery;
using Impersonate.Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery;

internal sealed class LocalTaskDeliveryRepairer(ITaskDeliveryRepository deliveries, ITaskDeliveryReviewRepository reviews, IRunDeliveryRepository runDeliveries, IPipelineRunRepository runs, IProjectRepository projects, IRepositoryWorkspaceService workspaces, RepositoryWorkspaceService concreteWorkspaces, IModelRouter router, ICoderAgent coder, IDeliveryValidationService validation, DeliveryWorkspaceRegistry deliveryWorkspaces, SafeProcess process, IOptions<ExecutionOptions> configured, ILogger<LocalTaskDeliveryRepairer> logger) : ITaskDeliveryRepairer
{
    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = await deliveries.ClaimNextRepairAsync(Guid.NewGuid(), workerId, now, now.AddMinutes(10), ct);
        if (delivery is null)
            return false;
        WorkspaceReference? workspace = null;
        DeliveryWorkspaceReference? validationReference = null;
        try
        {
            var run = await runs.GetAsync(delivery.ProjectId, delivery.PipelineRunId, ct);
            var task = run?.Tasks.SingleOrDefault(x => x.Id == delivery.PlannedTaskId);
            var project = await projects.GetAsync(delivery.ProjectId, ct);
            var review = (await reviews.ListAsync(delivery.Id, ct)).SingleOrDefault(x => x.IsCurrent);
            if (run is null || task is null || project is null || review is null || string.IsNullOrWhiteSpace(delivery.RemoteBranchName) || string.IsNullOrWhiteSpace(delivery.CommitSha))
            {
                delivery.Block("delivery_repair_context_missing", "Task delivery repair context is unavailable.", now);
                return true;
            }
            var prepared = await workspaces.PrepareAsync(new(delivery.ProjectId, delivery.PipelineRunId, delivery.PlannedTaskId, 10_000 + delivery.DeliveryRepairAttemptCount, project.RepositoryUrl, delivery.RemoteBranchName, [], null), ct);
            if (!prepared.Succeeded || prepared.Workspace is null)
            {
                delivery.ReleaseClaim();
                return true;
            }
            workspace = prepared.Workspace;
            if (!string.Equals(prepared.SourceBaseCommitSha, delivery.CommitSha, StringComparison.OrdinalIgnoreCase))
            {
                delivery.Block("delivery_repair_head_conflict", "The task branch advanced before repair could begin.", now);
                return true;
            }
            var path = concreteWorkspaces.FromReference(workspace);
            var resolvingConflict = delivery.Status == TaskDeliveryStatus.ConflictResolution;
            if (resolvingConflict)
            {
                var aggregate = await runDeliveries.GetByRunAsync(delivery.ProjectId, delivery.PipelineRunId, ct) ?? throw new InvalidOperationException("run_delivery_not_found");
                await Git(path, ["fetch", "--no-tags", "origin", $"+refs/heads/{aggregate.RunBranchName}:refs/remotes/origin/{aggregate.RunBranchName}"], ct);
                await Git(path, ["merge", "--no-commit", "--no-ff", $"refs/remotes/origin/{aggregate.RunBranchName}"], ct, allowFailure: true);
            }
            var feedback = resolvingConflict ? $"Resolve all integration conflicts against the current run branch. Prior approval: {review.Summary}" : review.Feedback ?? review.Summary;
            var selection = await router.SelectAsync(new(delivery.ProjectId, delivery.PipelineRunId, AgentRole.Coder, task.Description, task.CoderModelOverrideId, TaskTitle: task.Title, AcceptanceCriteria: task.AcceptanceCriteria, FeatureRequest: run.FeatureRequest, AttemptNumber: delivery.DeliveryRepairAttemptCount, RevisionCount: delivery.DeliveryRepairAttemptCount, ReviewerFeedback: feedback), ct);
            if (!selection.Succeeded || selection.Selection is null)
            {
                delivery.ReleaseClaim();
                return true;
            }
            var coded = await coder.ExecuteAsync(new(delivery.ProjectId, delivery.PipelineRunId, run.FeatureRequest, task.Id, task.Title, task.Description, task.AcceptanceCriteria, delivery.DeliveryRepairAttemptCount, delivery.DeliveryRepairAttemptCount, feedback, [], workspace, selection.Selection), ct);
            if (!coded.Succeeded)
            {
                delivery.ReleaseClaim();
                return true;
            }
            validationReference = deliveryWorkspaces.Register(path);
            var checkedResult = await validation.ValidateAsync(validationReference, ct);
            if (!checkedResult.Succeeded)
            {
                delivery.Block(checkedResult.Code ?? "delivery_repair_validation_failed", checkedResult.Error ?? "Task repair validation failed.", now);
                return true;
            }
            await Git(path, ["add", "-A", "--", "."], ct);
            var staged = await Git(path, ["diff", "--cached", "--quiet"], ct, allowFailure: true);
            if (staged.Succeeded)
            {
                delivery.Block("delivery_repair_no_changes", "The repair produced no changes.", now);
                return true;
            }
            await Git(path, ["-c", $"user.name={configured.Value.DeliveryCommitName}", "-c", $"user.email={configured.Value.DeliveryCommitEmail}", "commit", "-m", $"repair task {delivery.TaskSequence} after delivery review"], ct);
            var head = (await Git(path, ["rev-parse", "HEAD"], ct)).Output.Trim();
            await Git(path, ["push", "origin", $"HEAD:refs/heads/{delivery.RemoteBranchName}"], ct);
            delivery.RecordRepairCommit(head, JsonSerializer.Serialize(checkedResult.Value), now);
            delivery.ReleaseClaim();
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { delivery.ReleaseClaim(); throw; }
        catch { delivery.Block("delivery_repair_failed", "Task delivery repair failed safely."); return true; }
        finally
        {
            if (validationReference is not null)
                deliveryWorkspaces.Remove(validationReference);
            await deliveries.SaveChangesAsync(CancellationToken.None);
            if (workspace is not null)
                try { await workspaces.CleanupAsync(workspace, CancellationToken.None); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning("Delivery repair workspace cleanup was deferred."); }
        }
    }

    private async Task<ProcessResult> Git(string path, IReadOnlyList<string> args, CancellationToken ct, bool allowFailure = false)
    {
        var result = await process.RunAsync("git", args, path, configured.Value.CommandTimeoutSeconds, 4000, null, ct);
        if (!result.Succeeded && !allowFailure)
            throw new InvalidOperationException("delivery_repair_git_failed");
        return result;
    }
}
