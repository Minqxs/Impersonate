using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Delivery;
using Impersonate.Application.Execution;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Projects;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Delivery;
using Impersonate.Domain.Pipelines;
using Impersonate.Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery;

internal sealed class LocalFinalRunReviewer(IRunDeliveryRepository runDeliveries, IRunDeliveryReviewRepository reviews, ITaskDeliveryRepository taskDeliveries, IPipelineRunRepository runs, IProjectRepository projects, IRepositoryWorkspaceService workspaces, RepositoryWorkspaceService concreteWorkspaces, IModelRouter router, IReviewerAgent reviewer, ICoderAgent coder, IDeliveryValidationService validation, DeliveryWorkspaceRegistry deliveryWorkspaces, SafeProcess process, IOptions<ExecutionOptions> configured, ILogger<LocalFinalRunReviewer> logger) : IFinalRunReviewer
{
    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = await runDeliveries.ClaimNextFinalReviewAsync(Guid.NewGuid(), workerId, now, now.AddMinutes(10), ct);
        if (delivery is null)
            return false;
        WorkspaceReference? workspace = null;
        DeliveryWorkspaceReference? validationReference = null;
        try
        {
            var run = await runs.GetAsync(delivery.ProjectId, delivery.PipelineRunId, ct);
            var project = await projects.GetAsync(delivery.ProjectId, ct);
            if (run is null || project is null || string.IsNullOrWhiteSpace(delivery.RunBranchHeadSha))
            {
                delivery.Block("final_review_context_missing", "Final run review context is unavailable.", now);
                return true;
            }
            var tasks = await taskDeliveries.ListByRunAsync(delivery.ProjectId, delivery.PipelineRunId, ct);
            if (delivery.Status == RunDeliveryStatus.IntegratingTasks)
            {
                var expectedTaskIds = run.Tasks.Select(x => x.Id).ToHashSet();
                if (tasks.Count != expectedTaskIds.Count || tasks.Any(x => !expectedTaskIds.Contains(x.PlannedTaskId) || x.Status != TaskDeliveryStatus.MergedIntoRun))
                {
                    delivery.ReleaseClaim();
                    return true;
                }
                delivery.StartAggregateValidation(now);
                await runDeliveries.SaveChangesAsync(ct);
            }
            var prepared = await workspaces.PrepareAsync(new(delivery.ProjectId, delivery.PipelineRunId, delivery.Id, 20_000 + (await reviews.ListAsync(delivery.Id, ct)).Count, project.RepositoryUrl, delivery.RunBranchName, [], null), ct);
            if (!prepared.Succeeded || prepared.Workspace is null)
            {
                delivery.ReleaseClaim();
                return true;
            }
            workspace = prepared.Workspace;
            if (!string.Equals(prepared.SourceBaseCommitSha, delivery.RunBranchHeadSha, StringComparison.OrdinalIgnoreCase))
            {
                delivery.Block("final_review_head_conflict", "The run branch advanced outside the recorded delivery.", now);
                return true;
            }
            var path = concreteWorkspaces.FromReference(workspace);
            if (delivery.Status == RunDeliveryStatus.AggregateValidation)
            {
                validationReference = deliveryWorkspaces.Register(path);
                var checkedResult = await validation.ValidateAsync(validationReference, ct);
                if (!checkedResult.Succeeded)
                {
                    delivery.Block(checkedResult.Code ?? "aggregate_validation_failed", checkedResult.Error ?? "Aggregate validation failed.", now);
                    return true;
                }
                delivery.RecordAggregateValidation(JsonSerializer.Serialize(checkedResult.Value), now);
                delivery.ReleaseClaim();
                return true;
            }
            var history = await reviews.ListAsync(delivery.Id, ct);
            if (delivery.Status == RunDeliveryStatus.ChangesRequested)
            {
                if (history.Count >= 4)
                {
                    delivery.Block("final_review_attempts_exhausted", "The finite final review attempt limit was reached.", now);
                    return true;
                }
                var feedback = history.Last(x => x.IsCurrent).Feedback ?? history.Last(x => x.IsCurrent).Summary;
                var selected = await Select(delivery, run, AgentRole.Coder, feedback, history.Count + 1, ct);
                if (selected is null)
                {
                    delivery.ReleaseClaim();
                    return true;
                }
                var criteria = run.Tasks.SelectMany(x => x.AcceptanceCriteria).ToArray();
                var coded = await coder.ExecuteAsync(new(delivery.ProjectId, delivery.PipelineRunId, run.FeatureRequest, delivery.Id, "Aggregate run delivery", "Repair the integrated run branch after final review.", criteria, history.Count + 1, history.Count, feedback, run.Tasks.Select(x => x.Title).ToArray(), workspace, selected), ct);
                if (!coded.Succeeded)
                {
                    delivery.ReleaseClaim();
                    return true;
                }
                validationReference = deliveryWorkspaces.Register(path);
                var checkedResult = await validation.ValidateAsync(validationReference, ct);
                if (!checkedResult.Succeeded)
                {
                    delivery.Block(checkedResult.Code ?? "aggregate_repair_validation_failed", checkedResult.Error ?? "Aggregate repair validation failed.", now);
                    return true;
                }
                await Git(path, ["add", "-A", "--", "."], ct);
                var quiet = await Git(path, ["diff", "--cached", "--quiet"], ct, true);
                if (quiet.Succeeded)
                {
                    delivery.Block("aggregate_repair_no_changes", "Aggregate repair produced no changes.", now);
                    return true;
                }
                await Git(path, ["-c", $"user.name={configured.Value.DeliveryCommitName}", "-c", $"user.email={configured.Value.DeliveryCommitEmail}", "commit", "-m", "repair integrated run after final review"], ct);
                var head = (await Git(path, ["rev-parse", "HEAD"], ct)).Output.Trim();
                await Git(path, ["push", "origin", $"HEAD:refs/heads/{delivery.RunBranchName}"], ct);
                history.Last(x => x.IsCurrent).Supersede(now);
                delivery.ResumeFinalReview(head, now);
                delivery.ReleaseClaim();
                return true;
            }
            if (delivery.Status == RunDeliveryStatus.FinalReview)
            {
                var diff = (await Git(path, ["diff", $"{delivery.SourceBaseCommitSha}...HEAD", "--"], ct)).Output;
                if (string.IsNullOrWhiteSpace(diff))
                {
                    delivery.Block("final_review_diff_empty", "The integrated run has no reviewable diff.", now);
                    return true;
                }
                var selected = await Select(delivery, run, AgentRole.Reviewer, null, history.Count + 1, ct);
                if (selected is null)
                {
                    delivery.ReleaseClaim();
                    return true;
                }
                var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(diff))).ToLowerInvariant();
                var files = diff.Split('\n').Where(x => x.StartsWith("+++ b/", StringComparison.Ordinal)).Select(x => x[6..].Trim()).Distinct().ToArray();
                var result = await reviewer.ReviewAsync(new(delivery.ProjectId, delivery.PipelineRunId, run.FeatureRequest, delivery.Id, "Aggregate run delivery", "Review the complete integrated feature before its final pull request.", run.Tasks.SelectMany(x => x.AcceptanceCriteria).ToArray(), history.Count + 1, diff, sha, files, [delivery.AggregateValidationSummaryJson], "Integrated run", history.LastOrDefault()?.Feedback, workspace, selected), ct);
                if (!result.Succeeded || result.Decision is null)
                {
                    delivery.ReleaseClaim();
                    return true;
                }
                var decision = result.Decision == ReviewDecisionType.Approved ? DeliveryReviewDecision.Approved : DeliveryReviewDecision.ChangesRequested;
                var review = RunDeliveryReview.Create(delivery.Id, history.Count + 1, selected.ProviderType.ToString(), selected.ProviderModelId, delivery.RunBranchHeadSha, decision, result.Summary, JsonSerializer.Serialize(result.Findings), result.Feedback, now);
                await reviews.AddAsync(review, ct);
                if (decision == DeliveryReviewDecision.Approved)
                    delivery.ApproveFinalReview(review.Id, delivery.RunBranchHeadSha, now);
                else
                    delivery.RequestChanges(now);
                delivery.ReleaseClaim();
                return true;
            }
            delivery.ReleaseClaim();
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { delivery.ReleaseClaim(); throw; }
        catch { delivery.Block("final_review_failed", "Final run review failed safely."); return true; }
        finally
        {
            if (validationReference is not null)
                deliveryWorkspaces.Remove(validationReference);
            await reviews.SaveChangesAsync(CancellationToken.None);
            await runDeliveries.SaveChangesAsync(CancellationToken.None);
            if (workspace is not null)
                try
                {
                    await workspaces.CleanupAsync(workspace, CancellationToken.None);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogWarning("Final review workspace cleanup was deferred."); }
        }
    }
    private async Task<SelectedModel?> Select(RunDelivery delivery, PipelineRun run, AgentRole role, string? feedback, int attempt, CancellationToken ct)
    {
        var result = await router.SelectAsync(new(delivery.ProjectId, delivery.PipelineRunId, role, "Integrated run delivery", TaskTitle: "Aggregate run delivery", AcceptanceCriteria: run.Tasks.SelectMany(x => x.AcceptanceCriteria).ToArray(), FeatureRequest: run.FeatureRequest, AttemptNumber: attempt, RevisionCount: Math.Max(0, attempt - 1), ReviewerFeedback: feedback), ct);
        return result.Succeeded ? result.Selection : null;
    }
    private async Task<ProcessResult> Git(string path, IReadOnlyList<string> args, CancellationToken ct, bool allowFailure = false)
    {
        var result = await process.RunAsync("git", args, path, configured.Value.CommandTimeoutSeconds, 2_000_000, null, ct);
        if (!result.Succeeded && !allowFailure)
            throw new InvalidOperationException("final_review_git_failed");
        return result;
    }
}
