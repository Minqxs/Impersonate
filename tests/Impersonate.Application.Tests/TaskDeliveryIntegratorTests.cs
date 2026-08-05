using Impersonate.Application.Delivery;
using Impersonate.Application.Pipelines;
using Impersonate.Domain.Delivery;
using Xunit;

namespace Impersonate.Application.Tests;

public sealed class TaskDeliveryIntegratorTests
{
    [Fact]
    public async Task Exact_head_approval_is_merged_and_advances_run_head()
    {
        var delivery = Delivery("commit");
        var review = TaskDeliveryReview.Create(delivery.Id, 1, "Fake", "reviewer", "commit", DeliveryReviewDecision.Approved, "approved", "[]");
        var aggregate = Aggregate(delivery);
        var service = new TaskDeliveryIntegrator(new Deliveries(delivery), new Reviews(review), new RunDeliveries(aggregate), new Gateway());

        Assert.True(await service.ProcessOneAsync("worker", default));
        Assert.Equal(TaskDeliveryStatus.MergedIntoRun, delivery.Status);
        Assert.Equal("merge", aggregate.RunBranchHeadSha);
    }

    [Fact]
    public async Task Stale_approval_blocks_without_calling_merge()
    {
        var delivery = Delivery("commit");
        var review = TaskDeliveryReview.Create(delivery.Id, 1, "Fake", "reviewer", "old", DeliveryReviewDecision.Approved, "approved", "[]");
        var gateway = new Gateway();
        var service = new TaskDeliveryIntegrator(new Deliveries(delivery), new Reviews(review), new RunDeliveries(Aggregate(delivery)), gateway);

        await service.ProcessOneAsync("worker", default);
        Assert.Equal(TaskDeliveryStatus.Blocked, delivery.Status);
        Assert.Equal(0, gateway.MergeCalls);
    }

    [Fact]
    public async Task Merge_conflict_returns_delivery_to_repair_loop()
    {
        var delivery = Delivery("commit");
        var review = TaskDeliveryReview.Create(delivery.Id, 1, "Fake", "reviewer", "commit", DeliveryReviewDecision.Approved, "approved", "[]");
        var service = new TaskDeliveryIntegrator(new Deliveries(delivery), new Reviews(review), new RunDeliveries(Aggregate(delivery)), new Gateway(true));

        await service.ProcessOneAsync("worker", default);
        Assert.Equal(TaskDeliveryStatus.ConflictResolution, delivery.Status);
    }

    private static TaskDelivery Delivery(string commit)
    {
        var value = TaskDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "base", "artifact", "patch", Guid.NewGuid());
        value.StartPreparing();
        value.RecordDeliveryBase("base");
        value.RecordBranchPrepared("task/one");
        value.RecordPatchApplied();
        value.RecordValidated();
        value.RecordCommitted(commit);
        value.RecordPushed("origin", "owner/repo", "task/one", commit);
        value.RecordPullRequestOpen("GitHubMCP:test", "owner/repo", 1, "https://github.com/owner/repo/pull/1", "task/one", "run/one", commit, DateTimeOffset.UtcNow);
        value.StartDeliveryReview();
        value.ApproveForIntegration();
        return value;
    }
    private static RunDelivery Aggregate(TaskDelivery delivery)
    {
        var value = RunDelivery.Create(delivery.ProjectId, delivery.PipelineRunId, "main", "base", "run/one");
        value.StartPreparing();
        value.RecordRunBranch("base");
        value.StartTaskIntegration();
        return value;
    }
    private sealed class Gateway(bool conflicts = false) : IPullRequestGateway
    {
        public int MergeCalls
        {
            get; private set;
        }
        public Task<DeliveryOperationResult<PullRequestObservation>> MergeAsync(TaskDelivery delivery, CancellationToken ct)
        {
            MergeCalls++;
            return Task.FromResult(DeliveryOperationResult<PullRequestObservation>.Ok(new("GitHubMCP:test", "owner/repo", 1, "task/one", "run/one", delivery.CommitSha!, conflicts ? PullRequestExternalState.Open : PullRequestExternalState.Merged, conflicts ? null : "merge", conflicts)));
        }
        public Task<DeliveryOperationResult<PullRequestReference>> OpenAsync(TaskDelivery delivery, ApprovedTaskHandoff handoff, CancellationToken ct) => throw new NotSupportedException();
        public Task<DeliveryOperationResult<PullRequestObservation>> ReadAsync(TaskDelivery delivery, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Deliveries(TaskDelivery delivery) : ITaskDeliveryRepository
    {
        public Task<TaskDelivery?> ClaimNextIntegrationAsync(Guid id, string owner, DateTimeOffset at, DateTimeOffset expires, CancellationToken ct)
        {
            delivery.Claim(id, owner, expires, at);
            return Task.FromResult<TaskDelivery?>(delivery);
        }
        public Task<TaskDelivery?> GetByTaskAsync(Guid p, Guid r, Guid t, CancellationToken ct) => Task.FromResult<TaskDelivery?>(delivery);
        public Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<IReadOnlyList<TaskDelivery>>([delivery]);
        public Task AddAsync(TaskDelivery value, CancellationToken ct) => Task.CompletedTask;
        public Task<TaskDelivery?> ClaimNextPendingAsync(Guid a, string b, DateTimeOffset c, DateTimeOffset d, CancellationToken ct) => Task.FromResult<TaskDelivery?>(null);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Reviews(TaskDeliveryReview review) : ITaskDeliveryReviewRepository
    {
        public Task<IReadOnlyList<TaskDeliveryReview>> ListAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<TaskDeliveryReview>>([review]);
        public Task AddAsync(TaskDeliveryReview value, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class RunDeliveries(RunDelivery delivery) : IRunDeliveryRepository
    {
        public Task<RunDelivery?> GetByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<RunDelivery?>(delivery);
        public Task AddAsync(RunDelivery value, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
