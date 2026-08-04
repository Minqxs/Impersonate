using Impersonate.Application.Delivery;
using Impersonate.Application.Pipelines;
using Impersonate.Domain.Delivery;
using Impersonate.Domain.Pipelines;
using Xunit;

namespace Impersonate.Application.Tests;

public sealed class TaskDeliveryReconcilerTests
{
    [Fact]
    public async Task Open_pr_remains_awaiting_and_closed_pr_blocks()
    {
        var open = Fixture(PullRequestExternalState.Open);
        Assert.True(await open.Reconciler.ProcessOneAsync("worker", default));
        Assert.Equal(TaskDeliveryStatus.AwaitingMerge, open.Delivery.Status);
        Assert.Null(open.Delivery.ClaimId);

        var closed = Fixture(PullRequestExternalState.Closed);
        await closed.Reconciler.ProcessOneAsync("worker", default);
        Assert.Equal(TaskDeliveryStatus.Blocked, closed.Delivery.Status);
        Assert.Equal("delivery_pull_request_closed", closed.Delivery.FailureCode);
    }

    [Fact]
    public async Task Merged_pr_marks_delivery_and_completes_run()
    {
        var fixture = Fixture(PullRequestExternalState.Merged);
        await fixture.Reconciler.ProcessOneAsync("worker", default);
        Assert.Equal(TaskDeliveryStatus.Merged, fixture.Delivery.Status);
        Assert.Equal(PipelineRunStatus.Completed, fixture.Run.Status);
        Assert.Equal(LoopRunStatus.Completed, fixture.Run.LoopRun.Status);
    }

    [Fact]
    public async Task Merged_deliveries_complete_with_skipped_tasks()
    {
        var fixture = Fixture(PullRequestExternalState.Merged, includeSkippedTask: true);
        await fixture.Reconciler.ProcessOneAsync("worker", default);
        Assert.Equal(PipelineRunStatus.CompletedWithSkippedTasks, fixture.Run.Status);
    }

    [Fact]
    public async Task Changed_head_blocks_and_transient_failure_keeps_checkpoint()
    {
        var changed = Fixture(PullRequestExternalState.Open, "delivery_pull_request_head_changed");
        await changed.Reconciler.ProcessOneAsync("worker", default);
        Assert.Equal(TaskDeliveryStatus.Blocked, changed.Delivery.Status);

        var transient = Fixture(PullRequestExternalState.Open, "github_mcp_unavailable");
        await transient.Reconciler.ProcessOneAsync("worker", default);
        Assert.Equal(TaskDeliveryStatus.AwaitingMerge, transient.Delivery.Status);
        Assert.Null(transient.Delivery.ClaimId);
    }

    private static TestFixture Fixture(PullRequestExternalState state, string? failure = null, bool includeSkippedTask = false)
    {
        var run = PipelineRun.Create(Guid.NewGuid(), "feature");
        run.StartPlanning();
        var task = run.AddTask(1, "Task", "Work");
        var skipped = includeSkippedTask ? run.AddTask(2, "Skipped", "Skipped work") : null;
        run.MarkReadyForExecution();
        typeof(PlannedTask).GetProperty(nameof(PlannedTask.Status))!.SetValue(task, PlannedTaskStatus.Approved);
        if (skipped is not null)
            typeof(PlannedTask).GetProperty(nameof(PlannedTask.Status))!.SetValue(skipped, PlannedTaskStatus.Skipped);
        typeof(PipelineRun).GetProperty(nameof(PipelineRun.Status))!.SetValue(run, PipelineRunStatus.ReadyForDelivery);
        typeof(LoopRun).GetProperty(nameof(LoopRun.CurrentStage))!.SetValue(run.LoopRun, LoopStage.Committing);
        var delivery = TaskDelivery.Create(run.ProjectId, run.Id, task.Id, 1, "base", "artifact", "patch", Guid.NewGuid());
        delivery.StartPreparing();
        delivery.RecordDeliveryBase("base");
        delivery.RecordBranchPrepared("feature/task");
        delivery.RecordPatchApplied();
        delivery.RecordValidated();
        delivery.RecordCommitted("commit");
        delivery.RecordPushed("origin", "owner/repo", "feature/task", "commit");
        delivery.RecordPullRequestOpen("GitHubMCP:test", "owner/repo", 1, "https://github.com/owner/repo/pull/1", "feature/task", "main", "commit", DateTimeOffset.UtcNow);
        delivery.AwaitMerge();
        ((List<TaskDelivery>)typeof(PipelineRun).GetField("deliveries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(run)!).Add(delivery);
        var repository = new Deliveries(delivery);
        return new(run, delivery, new TaskDeliveryReconciler(repository, new Runs(run), new Gateway(state, failure)));
    }

    private sealed record TestFixture(PipelineRun Run, TaskDelivery Delivery, ITaskDeliveryReconciler Reconciler);
    private sealed class Gateway(PullRequestExternalState state, string? failure) : IPullRequestGateway
    {
        public Task<DeliveryOperationResult<PullRequestReference>> OpenAsync(TaskDelivery d, ApprovedTaskHandoff h, CancellationToken ct) => throw new NotSupportedException();
        public Task<DeliveryOperationResult<PullRequestObservation>> ReadAsync(TaskDelivery d, CancellationToken ct) => Task.FromResult(failure is null ? DeliveryOperationResult<PullRequestObservation>.Ok(new("GitHubMCP:test", "owner/repo", 1, "feature/task", "main", "commit", state, state == PullRequestExternalState.Merged ? "merge" : null)) : DeliveryOperationResult<PullRequestObservation>.Fail(failure, "safe failure"));
    }
    private sealed class Deliveries(TaskDelivery delivery) : ITaskDeliveryRepository
    {
        public Task<TaskDelivery?> ClaimNextReconciliationAsync(Guid id, string owner, DateTimeOffset at, DateTimeOffset expires, CancellationToken ct)
        {
            delivery.Claim(id, owner, expires, at);
            return Task.FromResult<TaskDelivery?>(delivery);
        }
        public Task<TaskDelivery?> ClaimNextPendingAsync(Guid a, string b, DateTimeOffset c, DateTimeOffset d, CancellationToken ct) => Task.FromResult<TaskDelivery?>(null);
        public Task<TaskDelivery?> GetByTaskAsync(Guid p, Guid r, Guid t, CancellationToken ct) => Task.FromResult<TaskDelivery?>(delivery); public Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<IReadOnlyList<TaskDelivery>>([delivery]); public Task AddAsync(TaskDelivery d, CancellationToken ct) => Task.CompletedTask; public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Runs(PipelineRun run) : IPipelineRunRepository
    {
        public Task<PipelineRun?> GetAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<PipelineRun?>(run); public Task AddAsync(PipelineRun r, CancellationToken ct) => Task.CompletedTask; public Task<PipelineRun?> ClaimNextExecutionAsync(Guid a, string b, DateTimeOffset c, DateTimeOffset d, CancellationToken ct) => Task.FromResult<PipelineRun?>(null); public Task<IReadOnlyList<PlanningAttempt>> GetPlanningAttemptsAsync(Guid r, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlanningAttempt>>([]); public Task<IReadOnlyList<PipelineRun>> ListAsync(Guid p, PipelineRunStatus? s, DateTimeOffset? f, DateTimeOffset? t, CancellationToken ct) => Task.FromResult<IReadOnlyList<PipelineRun>>([run]); public Task DeleteAsync(Guid p, Guid r, CancellationToken ct) => Task.CompletedTask; public void RemoveTransientAttempt(TaskAttempt a)
        {
        }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
