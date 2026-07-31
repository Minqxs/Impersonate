using Impersonate.Application.Ai;
using Impersonate.Application.Delivery;
using Impersonate.Application.Pipelines;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Delivery;
using Impersonate.Domain.Pipelines;
using Xunit;

namespace Impersonate.Application.Tests;

public sealed class TaskDeliveryCoordinatorTests
{
    [Fact]
    public async Task Two_independent_approved_tasks_create_two_deliveries_not_one_run_delivery()
    {
        var fixture = Fixture.Create(twoTasks: true, dependent: false);
        var first = await fixture.Coordinator.GetOrCreateAsync(fixture.Run.ProjectId, fixture.Run.Id, fixture.Run.Tasks[0].Id, default);
        var replay = await fixture.Coordinator.GetOrCreateAsync(fixture.Run.ProjectId, fixture.Run.Id, fixture.Run.Tasks[0].Id, default);
        var second = await fixture.Coordinator.GetOrCreateAsync(fixture.Run.ProjectId, fixture.Run.Id, fixture.Run.Tasks[1].Id, default);
        Assert.True(first.Succeeded);
        Assert.Same(first.Value, replay.Value);
        Assert.True(second.Succeeded);
        Assert.Equal(2, fixture.Deliveries.Items.Count);
        Assert.All(fixture.Deliveries.Items, x => Assert.NotEqual(Guid.Empty, x.PlannedTaskId));
        Assert.Equal(PipelineRunStatus.ReadyForDelivery, fixture.Run.Status);
        Assert.Equal(LoopStage.Committing, fixture.Run.LoopRun.CurrentStage);
    }

    [Fact]
    public async Task Dependent_task_waits_for_merged_delivery()
    {
        var fixture = Fixture.Create(twoTasks: true, dependent: true);
        var eligibility = await fixture.Coordinator.GetEligibilityAsync(fixture.Run.ProjectId, fixture.Run.Id, default);
        Assert.False(eligibility[1].Eligible);
        Assert.Equal([fixture.Run.Tasks[0].Id], eligibility[1].BlockingDependencyIds);
        var first = (await fixture.Coordinator.GetOrCreateAsync(fixture.Run.ProjectId, fixture.Run.Id, fixture.Run.Tasks[0].Id, default)).Value!;
        Assert.False((await fixture.Coordinator.GetEligibilityAsync(fixture.Run.ProjectId, fixture.Run.Id, default))[1].Eligible);
        Merge(first);
        Assert.True((await fixture.Coordinator.GetEligibilityAsync(fixture.Run.ProjectId, fixture.Run.Id, default))[1].Eligible);
    }

    [Fact]
    public async Task Run_completes_only_after_every_approved_delivery_is_merged()
    {
        var fixture = Fixture.Create(twoTasks: true);
        var first = (await fixture.Coordinator.GetOrCreateAsync(fixture.Run.ProjectId, fixture.Run.Id, fixture.Run.Tasks[0].Id, default)).Value!;
        var second = (await fixture.Coordinator.GetOrCreateAsync(fixture.Run.ProjectId, fixture.Run.Id, fixture.Run.Tasks[1].Id, default)).Value!;
        var navigation = (List<TaskDelivery>)typeof(PipelineRun).GetField("deliveries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(fixture.Run)!;
        navigation.AddRange([first, second]);
        Merge(first);
        Assert.Throws<InvalidOperationException>(() => fixture.Run.CompleteDelivery());
        Merge(second);
        fixture.Run.CompleteDelivery();
        Assert.Equal(PipelineRunStatus.Completed, fixture.Run.Status);
        Assert.Equal(LoopRunStatus.Completed, fixture.Run.LoopRun.Status);
    }

    [Fact]
    public async Task Changed_patch_cannot_reuse_existing_delivery()
    {
        var fixture = Fixture.Create();
        fixture.Deliveries.Items.Add(TaskDelivery.Create(fixture.Run.ProjectId, fixture.Run.Id, fixture.Run.Tasks[0].Id, 1, "base", "artifact:old", "old", Guid.NewGuid()));
        var result = await fixture.Coordinator.GetOrCreateAsync(fixture.Run.ProjectId, fixture.Run.Id, fixture.Run.Tasks[0].Id, default);
        Assert.False(result.Succeeded);
        Assert.Equal("delivery_identity_conflict", result.Code);
    }

    [Fact]
    public async Task Handoff_rejects_reviewed_patch_mismatch_and_missing_artifact_or_base()
    {
        var mismatch = Fixture.Create(reviewedSha: "different");
        Assert.Equal("reviewed_patch_mismatch", (await mismatch.Coordinator.BuildHandoffAsync(mismatch.Run.ProjectId, mismatch.Run.Id, mismatch.Run.Tasks[0].Id, default)).Code);
        var artifact = Fixture.Create(patchReference: " ");
        Assert.Equal("patch_artifact_missing", (await artifact.Coordinator.BuildHandoffAsync(artifact.Run.ProjectId, artifact.Run.Id, artifact.Run.Tasks[0].Id, default)).Code);
        var source = Fixture.Create(sourceSha: " ");
        Assert.Equal("source_base_missing", (await source.Coordinator.BuildHandoffAsync(source.Run.ProjectId, source.Run.Id, source.Run.Tasks[0].Id, default)).Code);
    }

    [Theory]
    [InlineData(PlannedTaskStatus.Skipped)]
    [InlineData(PlannedTaskStatus.Failed)]
    public async Task Skipped_and_failed_tasks_do_not_create_deliveries(PlannedTaskStatus status)
    {
        var fixture = Fixture.Create();
        var task = fixture.Run.Tasks[0];
        typeof(PlannedTask).GetProperty(nameof(PlannedTask.Status))!.SetValue(task, status);
        var result = await fixture.Coordinator.GetOrCreateAsync(fixture.Run.ProjectId, fixture.Run.Id, task.Id, default);
        Assert.False(result.Succeeded);
        Assert.Equal("task_not_approved", result.Code);
        Assert.Empty(fixture.Deliveries.Items);
    }

    private static void Merge(TaskDelivery delivery)
    {
        delivery.StartPreparing();
        delivery.RecordDeliveryBase("base");
        delivery.RecordBranchPrepared("feature/task");
        delivery.RecordPatchApplied();
        delivery.RecordValidated();
        delivery.RecordCommitted("commit");
        delivery.RecordPushed("origin", "owner/repo", "feature/task", "commit");
        delivery.RecordPullRequestOpen("GitHub", "owner/repo", 1, "https://github.com/owner/repo/pull/1", "feature/task", "main", "commit", DateTimeOffset.UtcNow);
        delivery.MarkMerged();
    }

    private sealed class Fixture
    {
        public required PipelineRun Run
        {
            get; init;
        }
        public required FakeDeliveries Deliveries
        {
            get; init;
        }
        public required ITaskDeliveryCoordinator Coordinator
        {
            get; init;
        }
        public static Fixture Create(bool twoTasks = false, bool dependent = false, string reviewedSha = "patch", string patchReference = "artifact:patch", string sourceSha = "base")
        {
            var run = PipelineRun.Create(Guid.NewGuid(), "feature");
            run.StartPlanning();
            var first = run.AddTask(1, "First", "First task");
            PlannedTask? second = twoTasks ? run.AddTask(2, "Second", "Second task") : null;
            first.SetIntelligence([], [], "Code", "Low", "Low", "first", [], 1, false, null, false);
            second?.SetIntelligence(dependent ? [first.Id] : [], [], "Code", "Low", "Low", "second", [], 2, false, null, false);
            run.MarkReadyForExecution();
            run.StartExecution();
            var routing = new FakeRouting();
            Approve(run, first, routing, patchReference, sourceSha, reviewedSha);
            if (second is not null)
                Approve(run, second, routing, "artifact:patch2", "base", "patch2", "patch2");
            var runs = new FakeRuns(run);
            var deliveries = new FakeDeliveries();
            return new()
            {
                Run = run,
                Deliveries = deliveries,
                Coordinator = new TaskDeliveryCoordinator(runs, deliveries, routing)
            };
        }
        private static void Approve(PipelineRun run, PlannedTask task, FakeRouting routing, string artifact, string source, string reviewSha, string patchSha = "patch")
        {
            var attempt = run.ClaimNextTask(Guid.NewGuid(), "worker", DateTimeOffset.UtcNow.AddMinutes(1));
            Assert.Same(task, attempt);
            var record = task.Attempts[^1];
            record.RecordComposition(string.IsNullOrWhiteSpace(source) ? "base" : source, [], "tree", false);
            record.RecordExecution("CoderProvider", "coder", "v1", null, 1, 1, 1, "[]", string.IsNullOrWhiteSpace(artifact) ? "artifact:temporary" : artifact, patchSha, "[]");
            if (string.IsNullOrWhiteSpace(artifact))
                typeof(TaskAttempt).GetProperty(nameof(TaskAttempt.PatchArtifactReference))!.SetValue(record, null);
            if (string.IsNullOrWhiteSpace(source))
                typeof(TaskAttempt).GetProperty(nameof(TaskAttempt.SourceBaseCommitSha))!.SetValue(record, null);
            task.CompleteAttempt("done");
            run.MoveTaskToReview(task);
            var review = run.RecordReview(task, ReviewDecisionType.Approved, "approved");
            review.RecordExecution("ReviewerProvider", "reviewer", "v1", null, 1, 1, reviewSha, "[]");
            routing.Add(run.ProjectId, run.Id, task.Id, record.Id);
            run.FinishApprovedTask(task);
        }
    }

    private sealed class FakeRuns(PipelineRun run) : IPipelineRunRepository
    {
        public Task<PipelineRun?> GetAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<PipelineRun?>(run);
        public Task AddAsync(PipelineRun r, CancellationToken ct) => Task.CompletedTask; public Task<PipelineRun?> ClaimNextExecutionAsync(Guid a, string b, DateTimeOffset c, DateTimeOffset d, CancellationToken ct) => Task.FromResult<PipelineRun?>(null);
        public Task<IReadOnlyList<PlanningAttempt>> GetPlanningAttemptsAsync(Guid r, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlanningAttempt>>([]); public Task<IReadOnlyList<PipelineRun>> ListAsync(Guid p, PipelineRunStatus? s, DateTimeOffset? f, DateTimeOffset? t, CancellationToken ct) => Task.FromResult<IReadOnlyList<PipelineRun>>([run]);
        public Task DeleteAsync(Guid p, Guid r, CancellationToken ct) => Task.CompletedTask; public void RemoveTransientAttempt(TaskAttempt a)
        {
        }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FakeDeliveries : ITaskDeliveryRepository
    {
        public List<TaskDelivery> Items { get; } = [];
        public Task<TaskDelivery?> GetByTaskAsync(Guid p, Guid r, Guid t, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.PlannedTaskId == t));
        public Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<IReadOnlyList<TaskDelivery>>(Items.ToList());
        public Task<TaskDelivery?> ClaimNextPendingAsync(Guid claimId, string owner, DateTimeOffset claimedAt, DateTimeOffset expiresAt, CancellationToken ct) => Task.FromResult<TaskDelivery?>(null);
        public Task AddAsync(TaskDelivery d, CancellationToken ct)
        {
            Items.Add(d);
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FakeRouting : IAiRoutingRepository
    {
        private readonly List<ModelSelectionDecision> decisions = [];
        public void Add(Guid p, Guid r, Guid t, Guid a)
        {
            decisions.Add(ModelSelectionDecision.Create(p, r, AgentRole.Coder, null, null, "CoderProvider", "coder", ModelSelectionSource.AutomaticRouting, 10, "{}", "coder", "[]", plannedTaskId: t, taskAttemptId: a));
            decisions.Add(ModelSelectionDecision.Create(p, r, AgentRole.Reviewer, null, null, "ReviewerProvider", "reviewer", ModelSelectionSource.AutomaticRouting, 10, "{}", "reviewer", "[]", plannedTaskId: t, taskAttemptId: a));
        }
        public Task<ModelSelectionDecision?> GetDecisionAsync(Guid p, Guid r, Guid a, AgentRole role, CancellationToken ct) => Task.FromResult(decisions.LastOrDefault(x => x.TaskAttemptId == a && x.Role == role));
        public Task<ModelSelectionDecision?> GetDecisionAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<ModelSelectionDecision?>(null); public Task<IReadOnlyList<AiProviderConnection>> GetConnectionsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AiProviderConnection>>([]); public Task<AiProviderConnection?> GetConnectionAsync(Guid id, CancellationToken ct) => Task.FromResult<AiProviderConnection?>(null); public Task<IReadOnlyList<DiscoveredModel>> GetModelsAsync(Guid? id, CancellationToken ct) => Task.FromResult<IReadOnlyList<DiscoveredModel>>([]); public Task<ProjectAiRoutingPolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => Task.FromResult<ProjectAiRoutingPolicy?>(null); public Task AddConnectionAsync(AiProviderConnection x, CancellationToken ct) => Task.CompletedTask; public Task AddModelAsync(DiscoveredModel x, CancellationToken ct) => Task.CompletedTask; public Task RemoveConnectionAsync(AiProviderConnection x, CancellationToken ct) => Task.CompletedTask; public Task<ProjectAiRoutingPolicy> GetOrCreatePolicyAsync(Guid id, CancellationToken ct) => Task.FromResult(ProjectAiRoutingPolicy.Create(id)); public Task AddDecisionAsync(ModelSelectionDecision x, CancellationToken ct) => Task.CompletedTask; public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
