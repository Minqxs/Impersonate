using Impersonate.Domain.Delivery;
using Xunit;

namespace Impersonate.Domain.Tests;

public sealed class RunDeliveryTests
{
    [Fact]
    public void Run_delivery_follows_guarded_aggregate_state_machine()
    {
        var delivery = Create();
        Assert.Throws<InvalidOperationException>(() => delivery.StartTaskIntegration());
        delivery.StartPreparing();
        delivery.RecordRunBranch("head-1");
        delivery.StartTaskIntegration();
        delivery.RecordIntegratedHead("head-2");
        delivery.StartAggregateValidation();
        delivery.RecordAggregateValidation("{\"passed\":true}");
        var review = Guid.NewGuid();
        delivery.ApproveFinalReview(review, "head-2");
        delivery.RecordFinalPullRequest("GitHubMCP", "owner/repo", 12, "https://github.com/owner/repo/pull/12", "head-2", "main");
        delivery.RecordMainReadiness("mergeable", "passed");
        delivery.RequestMerge();
        delivery.MarkMerged();
        Assert.Equal(RunDeliveryStatus.Merged, delivery.Status);
        Assert.NotNull(delivery.CompletedAtUtc);
    }

    [Fact]
    public void Stale_final_review_cannot_enable_delivery()
    {
        var delivery = Create();
        delivery.StartPreparing();
        delivery.RecordRunBranch("new-head");
        delivery.StartTaskIntegration();
        delivery.StartAggregateValidation();
        delivery.RecordAggregateValidation("[]");
        Assert.Throws<InvalidOperationException>(() => delivery.ApproveFinalReview(Guid.NewGuid(), "old-head"));
    }

    private static RunDelivery Create() => RunDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), "main", "base", "impersonate/run-example");
}
