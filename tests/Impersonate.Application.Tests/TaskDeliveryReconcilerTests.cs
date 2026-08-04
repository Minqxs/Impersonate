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
        Assert.Equal(TaskDeliveryStatus.DeliveryReview, open.Delivery.Status);
        Assert.Null(open.Delivery.ClaimId);

        var closed = Fixture(PullRequestExternalState.Closed);
        await closed.Reconciler.ProcessOneAsync("worker", default);
        Assert.Equal(TaskDeliveryStatus.Blocked, closed.Delivery.Status);
        Assert.Equal("delivery_pull_request_closed", closed.Delivery.FailureCode);
    }

    [Fact]
    public async Task Merged_pr_marks_delivery_integrated_and_updates_run_head()
    {
        var fixture = Fixture(PullRequestExternalState.Merged);
        await fixture.Reconciler.ProcessOneAsync("worker", default);
        Assert.Equal(TaskDeliveryStatus.MergedIntoRun, fixture.Delivery.Status);
        Assert.Equal(PipelineRunStatus.ReadyForDelivery, fixture.Run.Status);
        Assert.Equal("merge", fixture.RunDelivery.RunBranchHeadSha);
    }

    [Fact]
    public async Task Skipped_tasks_do_not_complete_before_final_run_delivery()
    {
        var fixture = Fixture(PullRequestExternalState.Merged, includeSkippedTask: true);
        await fixture.Reconciler.ProcessOneAsync("worker", default);
        Assert.Equal(PipelineRunStatus.ReadyForDelivery, fixture.Run.Status);
    }

    [Fact]
    public async Task Changed_head_blocks_and_transient_failure_keeps_checkpoint()
    {
        var changed = Fixture(PullRequestExternalState.Open, "delivery_pull_request_head_changed");
        await changed.Reconciler.ProcessOneAsync("worker", default);
        Assert.Equal(TaskDeliveryStatus.Blocked, changed.Delivery.Status);

        var transient = Fixture(PullRequestExternalState.Open, "github_mcp_unavailable");
        await transient.Reconciler.ProcessOneAsync("worker", default);
        Assert.Equal(TaskDeliveryStatus.DeliveryReview, transient.Delivery.Status);
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
        delivery.StartDeliveryReview();
        if (state == PullRequestExternalState.Merged)
        {
            delivery.ApproveForIntegration();
            delivery.RequestMerge();
        }
        ((List<TaskDelivery>)typeof(PipelineRun).GetField("deliveries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(run)!).Add(delivery);
        var repository = new Deliveries(delivery);
        var aggregate = RunDelivery.Create(run.ProjectId, run.Id, "main", "base", "impersonate/run-test");
        aggregate.StartPreparing();
        aggregate.RecordRunBranch("base");
        aggregate.StartTaskIntegration();
        return new(run, delivery, aggregate, new TaskDeliveryReconciler(repository, new RunDeliveries(aggregate), new Gateway(state, failure)));
    }

    private sealed record TestFixture(PipelineRun Run, TaskDelivery Delivery, RunDelivery RunDelivery, ITaskDeliveryReconciler Reconciler);
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
    private sealed class RunDeliveries(RunDelivery delivery) : IRunDeliveryRepository
    {
        public Task<RunDelivery?> GetByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<RunDelivery?>(delivery);
        public Task AddAsync(RunDelivery value, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
