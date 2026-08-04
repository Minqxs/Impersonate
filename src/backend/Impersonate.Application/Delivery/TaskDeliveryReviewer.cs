using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Application.Pipelines;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Delivery;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Delivery;

internal sealed class TaskDeliveryReviewer(ITaskDeliveryRepository deliveries, ITaskDeliveryReviewRepository reviews, IPullRequestGateway pullRequests, IPipelineRunRepository runs, IModelRouter router, IReviewerAgent reviewer) : ITaskDeliveryReviewer
{
    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = await deliveries.ClaimNextReviewAsync(Guid.NewGuid(), workerId, now, now.AddMinutes(5), ct);
        if (delivery is null)
            return false;
        try
        {
            var contextResult = await pullRequests.ReadReviewContextAsync(delivery, ct);
            if (!contextResult.Succeeded)
            {
                delivery.ReleaseClaim();
                return true;
            }
            var pull = contextResult.Value!;
            var history = await reviews.ListAsync(delivery.Id, ct);
            foreach (var prior in history.Where(x => x.IsCurrent && !string.Equals(x.ExactHeadSha, pull.HeadSha, StringComparison.OrdinalIgnoreCase)))
                prior.Supersede(now);
            var current = history.SingleOrDefault(x => x.IsCurrent && string.Equals(x.ExactHeadSha, pull.HeadSha, StringComparison.OrdinalIgnoreCase));
            if (current?.Decision == DeliveryReviewDecision.Approved)
            {
                delivery.ApproveForIntegration(now);
                delivery.ReleaseClaim();
                return true;
            }
            var run = await runs.GetAsync(delivery.ProjectId, delivery.PipelineRunId, ct);
            var task = run?.Tasks.SingleOrDefault(x => x.Id == delivery.PlannedTaskId);
            if (run is null || task is null)
            {
                delivery.Block("delivery_review_context_missing", "Pipeline task review context is unavailable.", now);
                return true;
            }
            if (history.Count >= Math.Max(1, task.MaximumRevisionAttempts + 1))
            {
                delivery.Block("delivery_review_attempts_exhausted", "The finite delivery review attempt limit was reached.", now);
                return true;
            }
            var selection = await router.SelectAsync(new(delivery.ProjectId, delivery.PipelineRunId, AgentRole.Reviewer, task.Description, task.ReviewerModelOverrideId, TaskTitle: task.Title, AcceptanceCriteria: task.AcceptanceCriteria, FeatureRequest: run.FeatureRequest, AttemptNumber: history.Count + 1, RevisionCount: delivery.DeliveryRepairAttemptCount, ExpectedFileCount: pull.ChangedFiles.Count, ExpectedDiffSize: pull.Diff.Length), ct);
            if (!selection.Succeeded || selection.Selection is null)
            {
                delivery.ReleaseClaim();
                return true;
            }
            var patchSha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pull.Diff))).ToLowerInvariant();
            var result = await reviewer.ReviewAsync(new(delivery.ProjectId, delivery.PipelineRunId, run.FeatureRequest, task.Id, task.Title, task.Description, task.AcceptanceCriteria, history.Count + 1, pull.Diff, patchSha, pull.ChangedFiles, [], "Task pull-request head review", current?.Summary, new($"delivery-pr:{delivery.Id:N}:{pull.HeadSha}"), selection.Selection), ct);
            if (!result.Succeeded || result.Decision is null)
            {
                delivery.ReleaseClaim();
                return true;
            }
            delivery.RecordDeliveryReviewAttempt(now);
            var decision = result.Decision == ReviewDecisionType.Approved ? DeliveryReviewDecision.Approved : DeliveryReviewDecision.ChangesRequested;
            await reviews.AddAsync(TaskDeliveryReview.Create(delivery.Id, history.Count + 1, selection.Selection.ProviderType.ToString(), selection.Selection.ProviderModelId, pull.HeadSha, decision, result.Summary, JsonSerializer.Serialize(result.Findings), result.Feedback, now), ct);
            if (decision == DeliveryReviewDecision.Approved)
                delivery.ApproveForIntegration(now);
            else
                delivery.RequestDeliveryChanges(now);
            delivery.ReleaseClaim();
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { delivery.ReleaseClaim(); throw; }
        finally { await reviews.SaveChangesAsync(CancellationToken.None); }
    }
}
