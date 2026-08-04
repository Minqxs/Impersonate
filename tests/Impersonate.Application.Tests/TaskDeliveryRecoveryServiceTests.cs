using Impersonate.Application.Delivery;
using Impersonate.Domain.Delivery;
using Xunit;

namespace Impersonate.Application.Tests;

public sealed class TaskDeliveryRecoveryServiceTests
{
    [Fact]
    public async Task Retry_reuses_delivery_branch_and_idempotency_without_creating_work()
    {
        var delivery = Blocked();
        var id = delivery.Id;
        var key = delivery.IdempotencyKey;
        var branch = delivery.BranchName;
        var repository = new Repository(delivery);
        var service = new TaskDeliveryRecoveryService(repository, new Coordinator(delivery));
        var result = await service.RetryAsync(delivery.ProjectId, delivery.PipelineRunId, delivery.Id, default);
        Assert.True(result.Succeeded);
        Assert.Same(delivery, result.Value);
        Assert.Equal(id, delivery.Id);
        Assert.Equal(key, delivery.IdempotencyKey);
        Assert.Equal(branch, delivery.BranchName);
        Assert.Equal(TaskDeliveryStatus.BranchPrepared, delivery.Status);
        Assert.Single(repository.Items);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Simultaneous_retries_cannot_both_recover_or_create_claims()
    {
        var delivery = Blocked();
        var repository = new Repository(delivery);
        var service = new TaskDeliveryRecoveryService(repository, new Coordinator(delivery));
        var results = await Task.WhenAll(service.RetryAsync(delivery.ProjectId, delivery.PipelineRunId, delivery.Id, default), service.RetryAsync(delivery.ProjectId, delivery.PipelineRunId, delivery.Id, default));
        Assert.Single(results, x => x.Succeeded);
        Assert.Single(results, x => !x.Succeeded);
        Assert.Single(repository.Items);
        Assert.Null(delivery.ClaimId);
    }

    private static TaskDelivery Blocked()
    {
        var delivery = TaskDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "base", "artifact:patch", "patch", Guid.NewGuid());
        delivery.StartPreparing();
        delivery.RecordDeliveryBase("base");
        delivery.RecordBranchIntent("impersonate/run/001-task-patch");
        delivery.RecordBranchPrepared(delivery.BranchName!);
        delivery.Block("delivery_patch_file_set_mismatch", "safe evidence");
        return delivery;
    }

    private sealed class Repository(TaskDelivery delivery) : ITaskDeliveryRepository
    {
        public List<TaskDelivery> Items { get; } = [delivery]; public int SaveCount
        {
            get; private set;
        }
        public Task<TaskDelivery?> GetByTaskAsync(Guid p, Guid r, Guid t, CancellationToken ct) => Task.FromResult<TaskDelivery?>(delivery);
        public Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<IReadOnlyList<TaskDelivery>>(Items);
        public Task AddAsync(TaskDelivery d, CancellationToken ct)
        {
            Items.Add(d);
            return Task.CompletedTask;
        }
        public Task<TaskDelivery?> ClaimNextPendingAsync(Guid id, string owner, DateTimeOffset at, DateTimeOffset expires, CancellationToken ct) => Task.FromResult<TaskDelivery?>(null);
        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
    private sealed class Coordinator(TaskDelivery delivery) : ITaskDeliveryCoordinator
    {
        public Task<DeliveryOperationResult<ApprovedTaskHandoff>> BuildHandoffAsync(Guid p, Guid r, Guid t, CancellationToken ct) => Task.FromResult(DeliveryOperationResult<ApprovedTaskHandoff>.Ok(new(p, r, t, 1, "task", "description", [], [], "base", "artifact:patch", delivery.ApprovedPatchSha256, ["file.cs"], [], delivery.ApprovedReviewDecisionId, "reviewer", "model", "approved", "coder", "model", new(Guid.NewGuid(), "auto", 1, "test", "v1", "[]"), new(Guid.NewGuid(), "auto", 1, "test", "v1", "[]"), Guid.NewGuid(), 1, 0)));
        public Task<DeliveryOperationResult<TaskDelivery>> GetOrCreateAsync(Guid p, Guid r, Guid t, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<DeliveryEligibility>> GetEligibilityAsync(Guid p, Guid r, CancellationToken ct) => throw new NotSupportedException();
    }
}
