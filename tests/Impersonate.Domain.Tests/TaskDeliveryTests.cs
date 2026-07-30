using Impersonate.Domain.Delivery;
using Xunit;

namespace Impersonate.Domain.Tests;

public sealed class TaskDeliveryTests
{
    [Fact]
    public void Delivery_follows_guarded_state_machine()
    {
        var delivery = Create();
        Assert.Throws<InvalidOperationException>(() => delivery.RecordCommitted("commit"));
        delivery.StartPreparing();
        delivery.RecordBranchPrepared("feature/task-1");
        delivery.RecordPatchApplied();
        delivery.RecordValidated();
        delivery.RecordCommitted("abc123");
        delivery.RecordPushed();
        delivery.RecordPullRequestOpen("GitHub", "owner/repo", 12, "https://github.com/owner/repo/pull/12");
        delivery.AwaitMerge();
        delivery.MarkMerged();
        Assert.Equal(TaskDeliveryStatus.Merged, delivery.Status);
        Assert.NotNull(delivery.CompletedAtUtc);
    }

    [Fact]
    public void Pull_request_requires_pushed_branch_and_merge_requires_identity()
    {
        var delivery = Create();
        Assert.Throws<InvalidOperationException>(() => delivery.RecordPullRequestOpen("GitHub", "owner/repo", 1, "safe"));
        Assert.Throws<InvalidOperationException>(() => delivery.MarkMerged());
    }

    [Fact]
    public void Failed_delivery_requires_explicit_recovery()
    {
        var delivery = Create();
        delivery.Fail("validation_failed", "Validation failed.");
        Assert.Throws<InvalidOperationException>(() => delivery.StartPreparing());
        delivery.Recover();
        delivery.StartPreparing();
        Assert.Equal(TaskDeliveryStatus.Preparing, delivery.Status);
    }

    [Fact]
    public void Idempotency_key_is_deterministic_and_patch_sensitive()
    {
        var project = Guid.NewGuid();
        var run = Guid.NewGuid();
        var task = Guid.NewGuid();
        Assert.Equal(TaskDelivery.BuildIdempotencyKey(project, run, task, "AA"), TaskDelivery.BuildIdempotencyKey(project, run, task, "aa"));
        Assert.NotEqual(TaskDelivery.BuildIdempotencyKey(project, run, task, "aa"), TaskDelivery.BuildIdempotencyKey(project, run, task, "bb"));
    }

    private static TaskDelivery Create() => TaskDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "base", "artifact:patch", "patch", Guid.NewGuid());
}
