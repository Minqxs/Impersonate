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
        delivery.RecordDeliveryBase("base");
        delivery.RecordBranchPrepared("feature/task-1");
        delivery.RecordPatchApplied();
        delivery.RecordValidated();
        delivery.RecordCommitted("abc123");
        delivery.RecordPushed("origin", "owner/repo", "feature/task-1", "abc123");
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

    [Fact]
    public void Claim_is_exclusive_until_expiry()
    {
        var delivery = Create();
        var now = DateTimeOffset.UtcNow;
        delivery.Claim(Guid.NewGuid(), "worker-1", now.AddMinutes(5), now);
        Assert.Throws<InvalidOperationException>(() => delivery.Claim(Guid.NewGuid(), "worker-2", now.AddMinutes(6), now.AddMinutes(1)));
        delivery.Claim(Guid.NewGuid(), "worker-2", now.AddMinutes(11), now.AddMinutes(6));
        Assert.Equal("worker-2", delivery.ClaimOwner);
        delivery.ReleaseClaim();
        Assert.Null(delivery.ClaimId);
    }

    [Fact]
    public void Branch_requires_a_resolved_delivery_base()
    {
        var delivery = Create();
        delivery.StartPreparing();
        Assert.Throws<InvalidOperationException>(() => delivery.RecordBranchPrepared("feature/task"));
    }

    [Fact]
    public void Push_identity_must_match_the_approved_commit()
    {
        var delivery = Create();
        delivery.StartPreparing(); delivery.RecordDeliveryBase("base"); delivery.RecordBranchPrepared("feature/task");
        delivery.RecordPatchApplied(); delivery.RecordValidated(); delivery.RecordCommitted("approved");
        Assert.Throws<InvalidOperationException>(() => delivery.RecordPushed("origin", "owner/repo", "feature/task", "different"));
    }

    [Fact]
    public void Recovery_resumes_the_pre_failure_checkpoint()
    {
        var delivery = Create();
        delivery.StartPreparing(); delivery.RecordDeliveryBase("base"); delivery.RecordBranchPrepared("feature/task");
        delivery.RecordPatchApplied(); delivery.RecordValidated(); delivery.RecordCommitted("approved");
        delivery.Block("delivery_push_failed", "Push failed.");
        delivery.Recover();
        Assert.Equal(TaskDeliveryStatus.Committed, delivery.Status);
    }

    private static TaskDelivery Create() => TaskDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "base", "artifact:patch", "patch", Guid.NewGuid());
}
