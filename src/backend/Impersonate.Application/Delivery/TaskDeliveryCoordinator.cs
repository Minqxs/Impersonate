using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Pipelines;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Delivery;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Delivery;

internal sealed class TaskDeliveryCoordinator(IPipelineRunRepository runs, ITaskDeliveryRepository deliveries, IAiRoutingRepository routing) : ITaskDeliveryCoordinator
{
    public async Task<DeliveryOperationResult<ApprovedTaskHandoff>> BuildHandoffAsync(Guid projectId, Guid runId, Guid taskId, CancellationToken ct)
    {
        var run = await runs.GetAsync(projectId, runId, ct);
        if (run is null)
            return Fail("run_not_found", "Pipeline run was not found.");
        if (run.Status != PipelineRunStatus.ReadyForDelivery)
            return Fail("run_not_ready_for_delivery", "Pipeline run must be ReadyForDelivery.");
        if (run.LoopRun.CurrentStage != LoopStage.Committing)
            return Fail("loop_not_committing", "Loop stage must be Committing.");
        if (run.ExecutionClaimId is not null || run.ExecutionClaimedTaskId is not null)
            return Fail("execution_claim_unresolved", "Execution claim must be cleared before delivery.");
        var task = run.Tasks.SingleOrDefault(x => x.Id == taskId);
        if (task is null)
            return Fail("task_not_found", "Planned task was not found.");
        if (task.Status != PlannedTaskStatus.Approved)
            return Fail("task_not_approved", "Only an approved task can be delivered.");
        var review = task.ReviewDecisions.SingleOrDefault(x => x.IsCurrent);
        if (review is null || review.Decision != ReviewDecisionType.Approved)
            return Fail("review_not_approved", "Current review must be approved.");
        var attempt = task.Attempts.SingleOrDefault(x => x.Id == review.TaskAttemptId);
        if (attempt is null || attempt.Status != TaskAttemptStatus.Completed)
            return Fail("approved_attempt_missing", "Approved task attempt is missing.");
        if (string.IsNullOrWhiteSpace(attempt.PatchArtifactReference))
            return Fail("patch_artifact_missing", "Approved patch artifact reference is required.");
        if (string.IsNullOrWhiteSpace(attempt.PatchSha256))
            return Fail("patch_sha_missing", "Approved patch SHA-256 is required.");
        if (string.IsNullOrWhiteSpace(review.ReviewedPatchSha256) || !string.Equals(review.ReviewedPatchSha256, attempt.PatchSha256, StringComparison.OrdinalIgnoreCase))
            return Fail("reviewed_patch_mismatch", "Reviewed patch SHA-256 must match the approved attempt patch SHA-256.");
        if (string.IsNullOrWhiteSpace(attempt.SourceBaseCommitSha))
            return Fail("source_base_missing", "Source base commit SHA is required.");
        if (string.IsNullOrWhiteSpace(attempt.Provider) || string.IsNullOrWhiteSpace(attempt.Model))
            return Fail("coder_identity_missing", "Coder provider and model are required.");
        if (string.IsNullOrWhiteSpace(review.Provider) || string.IsNullOrWhiteSpace(review.Model))
            return Fail("reviewer_identity_missing", "Reviewer provider and model are required.");
        var coder = await routing.GetDecisionAsync(projectId, runId, attempt.Id, AgentRole.Coder, ct);
        var reviewer = await routing.GetDecisionAsync(projectId, runId, attempt.Id, AgentRole.Reviewer, ct);
        if (coder is null || reviewer is null)
            return Fail("model_selection_evidence_missing", "Coder and Reviewer model-selection evidence are required.");
        var handoff = new ApprovedTaskHandoff(projectId, runId, task.Id, task.Sequence, task.Title, task.Description, task.AcceptanceCriteria,
            DeserializeGuids(task.DependsOnTaskIdsJson), attempt.SourceBaseCommitSha, attempt.PatchArtifactReference, attempt.PatchSha256,
            DeserializeStrings(attempt.ChangedFilesJson), DeserializeStrings(attempt.ValidationSummaryJson), review.Id, review.Provider, review.Model,
            review.Summary, attempt.Provider, attempt.Model, Evidence(coder), Evidence(reviewer), attempt.Id, attempt.AttemptNumber, task.RevisionCount);
        return DeliveryOperationResult<ApprovedTaskHandoff>.Ok(handoff);
    }

    public async Task<DeliveryOperationResult<TaskDelivery>> GetOrCreateAsync(Guid projectId, Guid runId, Guid taskId, CancellationToken ct)
    {
        var handoffResult = await BuildHandoffAsync(projectId, runId, taskId, ct);
        if (!handoffResult.Succeeded)
            return DeliveryOperationResult<TaskDelivery>.Fail(handoffResult.Code!, handoffResult.Error!);
        var handoff = handoffResult.Value!;
        var existing = await deliveries.GetByTaskAsync(projectId, runId, taskId, ct);
        if (existing is not null)
            return string.Equals(existing.ApprovedPatchSha256, handoff.ApprovedPatchSha256, StringComparison.OrdinalIgnoreCase)
                ? DeliveryOperationResult<TaskDelivery>.Ok(existing)
                : DeliveryOperationResult<TaskDelivery>.Fail("delivery_identity_conflict", "A delivery already exists for this task with a different approved patch SHA-256.");
        var eligibility = (await GetEligibilityAsync(projectId, runId, ct)).Single(x => x.PlannedTaskId == taskId);
        if (!eligibility.Eligible)
            return DeliveryOperationResult<TaskDelivery>.Fail("delivery_dependencies_blocked", $"Dependencies must be merged before delivery: {string.Join(", ", eligibility.BlockingDependencyIds)}.");
        var delivery = TaskDelivery.Create(projectId, runId, taskId, handoff.TaskSequence, handoff.SourceBaseCommitSha,
            handoff.ApprovedPatchArtifactReference, handoff.ApprovedPatchSha256, handoff.ApprovedReviewDecisionId);
        await deliveries.AddAsync(delivery, ct);
        await deliveries.SaveChangesAsync(ct);
        return DeliveryOperationResult<TaskDelivery>.Ok(delivery);
    }

    public async Task<IReadOnlyList<DeliveryEligibility>> GetEligibilityAsync(Guid projectId, Guid runId, CancellationToken ct)
    {
        var run = await runs.GetAsync(projectId, runId, ct);
        if (run is null)
            return [];
        var existing = (await deliveries.ListByRunAsync(projectId, runId, ct)).ToDictionary(x => x.PlannedTaskId);
        var runReady = run.Status == PipelineRunStatus.ReadyForDelivery && run.LoopRun.CurrentStage == LoopStage.Committing && run.ExecutionClaimId is null && run.ExecutionClaimedTaskId is null;
        return run.Tasks.OrderBy(x => x.Sequence).Select(task =>
        {
            var blockers = DeserializeGuids(task.DependsOnTaskIdsJson)
                .Where(id => !existing.TryGetValue(id, out var dependency) || dependency.Status != TaskDeliveryStatus.Merged).ToArray();
            var eligible = runReady && task.Status == PlannedTaskStatus.Approved && !existing.ContainsKey(task.Id) && blockers.Length == 0;
            return new DeliveryEligibility(task.Id, eligible, blockers);
        }).ToList();
    }

    private static ModelSelectionEvidence Evidence(ModelSelectionDecision decision) => new(decision.Id, decision.SelectionSource.ToString(), decision.Score, decision.Explanation, decision.MetadataVersion, decision.ScoreBreakdownJson);
    private static DeliveryOperationResult<ApprovedTaskHandoff> Fail(string code, string error) => DeliveryOperationResult<ApprovedTaskHandoff>.Fail(code, error);
    private static IReadOnlyList<string> DeserializeStrings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch { return []; }
    }
    private static IReadOnlyList<Guid> DeserializeGuids(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch { return []; }
    }
}
