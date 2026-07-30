using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Projects;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impersonate.Application.Execution;

internal sealed class TaskExecutionOrchestrator(IPipelineRunRepository runs, IProjectRepository projects, IModelRouter router, IAiRoutingRepository ai, IRepositoryWorkspaceService workspaces, IRepositoryTools tools, IExecutionArtifactStore artifacts, IExecutionInvocationStore invocations, ICoderAgent coder, IReviewerAgent reviewer, IModelIdentityClassifier identities, IOptions<ExecutionOptions> options, ILogger<TaskExecutionOrchestrator> logger) : ITaskExecutionOrchestrator
{
    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var run = await runs.ClaimNextExecutionAsync(Guid.NewGuid(), workerId, now, now.AddMinutes(options.Value.ClaimMinutes), ct);
        if (run is null)
            return false;
        var task = run.Tasks.Single(x => x.Id == run.ExecutionClaimedTaskId);
        var attempt = task.Attempts.Last();
        var project = await projects.GetAsync(run.ProjectId, ct);
        if (project is null)
        {
            await Fail(run, task, attempt, "workspace_preparation_failed", "Project was not found.", ct);
            return true;
        }

        var dependencyResult = ApprovedDependencyClosure(run.Tasks, task);
        if (!dependencyResult.Succeeded)
        {
            await BlockForInfrastructure(run, task, dependencyResult.FailureCode!, dependencyResult.FailureMessage!, ct);
            return true;
        }

        var priorApproved = dependencyResult.Tasks;
        var priorPatches = priorApproved.Select(x => new WorkspacePatchReference(x.Id, x.Sequence, x.Attempts.Last().PatchArtifactReference!)).ToList();
        var currentPatch = task.Status == PlannedTaskStatus.Reviewing ? attempt.PatchArtifactReference : attempt.AttemptType == TaskAttemptType.Revision ? task.Attempts.Where(x => x.AttemptNumber < attempt.AttemptNumber).LastOrDefault()?.PatchArtifactReference : null;
        var prepared = await workspaces.PrepareAsync(new(run.ProjectId, run.Id, task.Id, attempt.AttemptNumber, project.RepositoryUrl, project.DefaultBranch, priorPatches, currentPatch), ct);
        if (!prepared.Succeeded)
        {
            await BlockForInfrastructure(run, task, prepared.FailureCode!, prepared.FailureMessage!, ct);
            return true;
        }

        var workspace = prepared.Workspace!;
        attempt.RecordComposition(prepared.SourceBaseCommitSha!, prepared.DependencyTaskIds ?? [], prepared.ComposedTreeFingerprint!, prepared.CurrentRevisionPatchApplied);
        await runs.SaveChangesAsync(ct);
        var feedback = task.ReviewDecisions.LastOrDefault(x => x.Decision == ReviewDecisionType.ChangesRequested)?.Feedback;
        var scope = new ArtifactScope(run.ProjectId, run.Id, task.Id, attempt.AttemptNumber);
        CoderResult coded;
        StoredArtifact patch;
        string diffText;
        RoutingModelIdentity? selectedCoderIdentity = null;
        if (task.Status == PlannedTaskStatus.Reviewing)
        {
            if (attempt.PatchArtifactReference is null || attempt.PatchSha256 is null)
            {
                await Fail(run, task, attempt, "patch_generation_failed", "The persisted review patch is unavailable.", ct);
                return true;
            }

            diffText = await artifacts.ReadTextAsync(attempt.PatchArtifactReference, 2_000_000, ct);
            patch = new(attempt.PatchArtifactReference, attempt.PatchSha256, System.Text.Encoding.UTF8.GetByteCount(diffText), "text/x-diff", attempt.CompletedAtUtc ?? now);
            coded = new(true, attempt.Summary ?? "Persisted coding attempt", Deserialize(attempt.ChangedFilesJson), Deserialize(attempt.ValidationSummaryJson), attempt.ToolStepCount, attempt.ProviderRequestId, attempt.InputTokenCount, attempt.OutputTokenCount);
            var coderDecision = await ai.GetDecisionAsync(run.ProjectId, run.Id, attempt.Id, AgentRole.Coder, ct);
            var recordedModel = coderDecision is { Role: AgentRole.Coder, TaskAttemptId: var decisionAttempt, DiscoveredModelId: { } discovered } && decisionAttempt == attempt.Id
                ? (await ai.GetModelsAsync(null, ct)).SingleOrDefault(x => x.Id == discovered)
                : null;
            if (recordedModel is not null)
            {
                var identity = identities.Classify(recordedModel.ProviderType, recordedModel.ProviderModelId);
                selectedCoderIdentity = new(recordedModel.Id, recordedModel.ProviderType, recordedModel.ProviderModelId, identity.CanonicalFamily, identity.CanonicalFamily, identity.Variant.ToString());
            }
        }
        else
        {
            var excluded = new HashSet<Guid>();
            string? protocolSummary = null;
            ModelSelectionResult coderSelection;
            var fallback = 0;
            while (true)
            {
                coderSelection = await Select(run, task, attempt, AgentRole.Coder, task.CoderModelOverrideId, excluded, ct);
                if (!coderSelection.Succeeded)
                {
                    await Fail(run, task, attempt, "coder_provider_failed", coderSelection.FailureMessage ?? "No eligible Coder model is available.", ct);
                    return true;
                }

                var invocationStarted = DateTimeOffset.UtcNow;
                coded = await coder.ExecuteAsync(new(run.ProjectId, run.Id, run.FeatureRequest, task.Id, task.Title, task.Description, task.AcceptanceCriteria, attempt.AttemptNumber, task.RevisionCount, feedback, priorApproved.Select(x => x.Attempts.Last().Summary ?? x.Title).ToList(), workspace, coderSelection.Selection!, RepositoryEvidence: Deserialize(task.RepositoryEvidenceJson), PriorProtocolSummary: protocolSummary), ct);
                await invocations.AddAsync(ExecutionInvocation.Record(attempt.Id, fallback + 1, AgentRole.Coder.ToString(), coderSelection.Selection!.ProviderType.ToString(), coderSelection.Selection.ProviderModelId, await invocations.FindLatestSelectionDecisionIdAsync(attempt.Id, AgentRole.Coder, ct), "coder-v1", coded.ProviderRequestId, coded.InputTokenCount, coded.OutputTokenCount, coded.ResponseType, coded.ToolStepCount, coded.SuccessfulReadCount, coded.SuccessfulSearchCount, coded.SuccessfulPatchCount, fallback, coded.Succeeded, coded.FailureCode, coded.FailureMessage, invocationStarted, DateTimeOffset.UtcNow, coded.ProviderRoundTripCount, coded.ConsecutiveReadOnlyRounds, coded.MaximumSingleRequestInput, coded.ProviderResponseStatus, coded.ProviderIncompleteReason, coded.StructuredOutputRepairCount, coded.NoProgressCorrectionCount, coded.PaidProviderRequestCount, coded.CurrentPhase, coded.RequestedProhibitedTool, coded.PatchAttemptCount, coded.FailedPatchCount, coded.LastPatchFailureCode, coded.MaximumRequestedOutputReservation, JsonSerializer.Serialize(coded.OutputReservationReasons ?? []), coded.ProviderCapacityWaitMilliseconds, coded.ProviderResetUsed, coded.LastRateLimitScope), ct);
                await invocations.SaveChangesAsync(ct);
                if (coded.Succeeded)
                    break;
                protocolSummary = $"Previous model {coderSelection.Selection.ProviderModelId} failed with {coded.FailureCode}; response={coded.ResponseType}, tools={coded.ToolStepCount}, reads={coded.SuccessfulReadCount}, searches={coded.SuccessfulSearchCount}, patches={coded.SuccessfulPatchCount}.";
                if (task.CoderModelOverrideId is not null || !IsFallbackEligible(coded.FailureCode) || fallback++ >= options.Value.MaximumModelFallbacks || coderSelection.Selection is not { } failed)
                {
                    await Fail(run, task, attempt, coded.FailureCode ?? "coder_provider_failed", coded.FailureMessage ?? "Coder failed.", ct);
                    return true;
                }

                await ExcludeFamily(excluded, failed, ct);
            }

            var diff = await tools.GetDiffAsync(workspace, ct);
            if (!diff.Succeeded || string.IsNullOrWhiteSpace(diff.Output))
            {
                await Fail(run, task, attempt, "patch_generation_failed", diff.FailureMessage ?? "No task patch was generated.", ct);
                return true;
            }

            diffText = diff.Output;
            patch = await artifacts.WriteTextAsync(scope, "task.patch", diffText, "text/x-diff", ct);
            attempt.RecordExecution(coderSelection.Selection!.ProviderType.ToString(), coderSelection.Selection.ProviderModelId, "coder-v1", coded.ProviderRequestId, coded.InputTokenCount, coded.OutputTokenCount, coded.ToolStepCount, JsonSerializer.Serialize(coded.ChangedFiles), patch.Reference, patch.Sha256, JsonSerializer.Serialize(coded.ValidationNotes));
            selectedCoderIdentity = ToIdentity(coderSelection.Selection);
            task.CompleteAttempt(coded.Summary);
            run.MoveTaskToReview(task);
            await runs.SaveChangesAsync(ct);
        }

        var reviewerExcluded = new HashSet<Guid>();
        ModelSelectionResult reviewerSelection;
        ReviewerResult reviewed;
        var reviewerFallback = 0;
        while (true)
        {
            reviewerSelection = await Select(run, task, attempt, AgentRole.Reviewer, task.ReviewerModelOverrideId, reviewerExcluded, ct, selectedCoderIdentity);
            if (!reviewerSelection.Succeeded)
            {
                await Fail(run, task, attempt, "reviewer_provider_failed", reviewerSelection.FailureMessage ?? "No eligible Reviewer model is available.", ct);
                return true;
            }

            reviewed = await reviewer.ReviewAsync(new(run.ProjectId, run.Id, run.FeatureRequest, task.Id, task.Title, task.Description, task.AcceptanceCriteria, attempt.AttemptNumber, diffText, patch.Sha256, coded.ChangedFiles, coded.ValidationNotes, coded.Summary, feedback, workspace, reviewerSelection.Selection!), ct);
            if (reviewed.Succeeded)
                break;
            if (task.ReviewerModelOverrideId is not null || !IsFallbackEligible(reviewed.FailureCode) || reviewerFallback++ >= options.Value.MaximumModelFallbacks || reviewerSelection.Selection is not { } failed)
            {
                await Fail(run, task, attempt, reviewed.FailureCode ?? "reviewer_provider_failed", reviewed.FailureMessage ?? "Reviewer failed.", ct);
                return true;
            }

            await ExcludeFamily(reviewerExcluded, failed, ct);
        }

        var decision = run.RecordReview(task, reviewed.Decision!.Value, reviewed.Summary, reviewed.Feedback);
        decision.RecordExecution(reviewerSelection.Selection!.ProviderType.ToString(), reviewerSelection.Selection.ProviderModelId, "reviewer-v1", reviewed.ProviderRequestId, reviewed.InputTokenCount, reviewed.OutputTokenCount, patch.Sha256, JsonSerializer.Serialize(reviewed.Findings));
        await artifacts.WriteTextAsync(scope, "reviewer-report.json", JsonSerializer.Serialize(reviewed), "application/json", ct);
        if (reviewed.Decision == ReviewDecisionType.Approved)
            run.FinishApprovedTask(task);
        else if (task.RevisionCount >= task.MaximumRevisionAttempts)
            run.ResolveRetryExhaustion(task, "Revision limit reached after Reviewer changes were requested.");
        else
            run.ClearExecutionClaim();
        await runs.SaveChangesAsync(ct);
        return true;
    }

    private async Task BlockForInfrastructure(PipelineRun run, PlannedTask task, string failureCode, string failureMessage, CancellationToken ct)
    {
        var rollback = run.BlockForInfrastructure(task, failureCode, failureMessage);
        runs.RemoveTransientAttempt(rollback.TransientAttempt);
        try
        {
            await runs.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new TaskExecutionPersistenceException(run.Id, task.Id, ex.GetType().Name);
        }
        logger.LogWarning(
            "Infrastructure rollback persisted for pipeline {PipelineRunId}, task {PlannedTaskId} sequence {TaskSequence}, transient attempt {TaskAttemptId} number {AttemptNumber} type {AttemptType}; failure {FailureCode}, run status {RunStatus}.",
            run.Id, rollback.PlannedTaskId, rollback.TaskSequence, rollback.AttemptId, rollback.AttemptNumber, rollback.AttemptType, failureCode, run.Status);
    }

    private async Task<ModelSelectionResult> Select(PipelineRun run, PlannedTask task, TaskAttempt attempt, AgentRole role, Guid? overrideId, IReadOnlySet<Guid> excluded, CancellationToken ct, RoutingModelIdentity? selectedCoderIdentity = null)
    {
        var languages = Deserialize(run.PlanningLanguagesJson);
        var frameworks = Deserialize(run.PlanningFrameworksJson);
        var areas = Deserialize(task.AffectedAreasJson);
        var request = new ModelSelectionRequest(run.ProjectId, run.Id, role, task.Description, overrideId, excluded, TaskTitle: task.Title, AcceptanceCriteria: task.AcceptanceCriteria, FeatureRequest: run.FeatureRequest, RepositoryLanguages: languages, RepositoryFrameworks: frameworks, ChangeType: task.ChangeType, AffectedAreas: areas, Risk: task.Risk, ConflictRisk: task.ConflictRisk, AttemptNumber: attempt.AttemptNumber, RevisionCount: task.RevisionCount, ReviewerFeedback: task.ReviewDecisions.LastOrDefault(x => x.IsCurrent)?.Feedback, CoderIdentity: selectedCoderIdentity);
        var selected = await router.SelectAsync(request, ct);
        if (selected.Succeeded && selected.Selection is { } model)
        {
            await ai.AddDecisionAsync(ModelSelectionDecision.Create(run.ProjectId, run.Id, role, model.ConnectionId, model.DiscoveredModelId, model.ProviderType.ToString(), model.ProviderModelId, model.Source, model.Score, JsonSerializer.Serialize(selected.Profile), model.Explanation + (excluded.Count > 0 ? $" Selected after {excluded.Count} transient model failure(s)." : ""), JsonSerializer.Serialize(selected.EligibleAlternatives.Take(3)), plannedTaskId: task.Id, taskAttemptId: attempt.Id, scoreBreakdown: JsonSerializer.Serialize(model.ScoreBreakdown ?? []), metadataVersion: model.MetadataVersion), ct);
            await ai.SaveChangesAsync(ct);
        }

        return selected;
    }

    private static RoutingModelIdentity ToIdentity(SelectedModel model) => new(model.DiscoveredModelId, model.ProviderType, model.ProviderModelId, model.CanonicalFamily ?? "unknown", model.Generation ?? "unknown", model.Specialisation ?? "Unknown");

    private static bool IsFallbackEligible(string? code) => code is "provider_rate_limited" or "provider_timeout" or "provider_unavailable" or "provider_overloaded" or "provider_context_limit_exceeded" or "provider_refused";
    private async Task ExcludeFamily(HashSet<Guid> excluded, SelectedModel failed, CancellationToken ct)
    {
        foreach (var model in await ai.GetModelsAsync(null, ct))
            if (model.ProviderType == failed.ProviderType && ModelRateLimitFamily.Matches(model.ProviderType, model.ProviderModelId, failed.ProviderModelId))
                excluded.Add(model.Id);
    }

    private async Task Fail(PipelineRun run, PlannedTask task, TaskAttempt attempt, string code, string message, CancellationToken ct)
    {
        if (attempt.Status == TaskAttemptStatus.Started)
            attempt.Fail(code, message);
        run.ResolveExecutionFailure(task, $"{code}: {message}");
        await runs.SaveChangesAsync(ct);
    }

    private static DependencyClosureResult ApprovedDependencyClosure(IReadOnlyList<PlannedTask> tasks, PlannedTask current)
    {
        var byId = tasks.ToDictionary(x => x.Id);
        var ordered = new List<PlannedTask>();
        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        bool Visit(Guid id)
        {
            if (visited.Contains(id))
                return true;
            if (!byId.TryGetValue(id, out var dependency) || dependency.Status != PlannedTaskStatus.Approved || dependency.Attempts.LastOrDefault()?.PatchArtifactReference is null)
                return false;
            if (!visiting.Add(id))
                return false;
            foreach (var nested in DeserializeGuids(dependency.DependsOnTaskIdsJson).OrderBy(x => byId.TryGetValue(x, out var item) ? item.Sequence : int.MaxValue).ThenBy(x => x))
                if (!Visit(nested))
                    return false;
            visiting.Remove(id);
            visited.Add(id);
            ordered.Add(dependency);
            return true;
        }

        foreach (var id in DeserializeGuids(current.DependsOnTaskIdsJson).OrderBy(x => byId.TryGetValue(x, out var item) ? item.Sequence : int.MaxValue).ThenBy(x => x))
            if (!Visit(id))
                return new(false, [], "approved_dependency_patch_missing", "An approved dependency patch is unavailable or the dependency graph is invalid.");
        return new(true, ordered.OrderBy(x => x.Sequence).ThenBy(x => x.Id).ToList(), null, null);
    }

    private sealed record DependencyClosureResult(bool Succeeded, IReadOnlyList<PlannedTask> Tasks, string? FailureCode, string? FailureMessage);
    private static IReadOnlyList<string> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<Guid> DeserializeGuids(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
