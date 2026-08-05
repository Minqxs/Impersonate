using Impersonate.Domain.Delivery;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;
using Impersonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Impersonate.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RunDeliveryClaimSqlServerCollection
{
    public const string Name = "RunDelivery SQL Server claims";
}

[Collection(RunDeliveryClaimSqlServerCollection.Name)]
public sealed class RunDeliveryClaimConcurrencyTests
{
    [Fact]
    public async Task Concurrent_final_review_claimers_get_one_delivery_once()
    {
        if (!OperatingSystem.IsWindows())
            return;
        await using var database = await SqlDatabase.CreateAsync();
        var delivery = await database.SeedAsync(RunDeliveryStatus.IntegratingTasks);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => database.ClaimReviewAsync($"review-{index}")));

        var claimed = Assert.Single(results, x => x is not null);
        Assert.Equal(delivery.Id, claimed!.Id);
    }

    [Fact]
    public async Task Concurrent_final_pr_claimers_get_one_delivery_once()
    {
        if (!OperatingSystem.IsWindows())
            return;
        await using var database = await SqlDatabase.CreateAsync();
        var delivery = await database.SeedAsync(RunDeliveryStatus.ReadyForFinalPullRequest);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(index => database.ClaimFinalPrAsync($"final-pr-{index}")));

        var claimed = Assert.Single(results, x => x is not null);
        Assert.Equal(delivery.Id, claimed!.Id);
    }

    [Fact]
    public async Task Review_and_final_pr_claims_can_progress_concurrently()
    {
        if (!OperatingSystem.IsWindows())
            return;
        await using var database = await SqlDatabase.CreateAsync();
        var review = await database.SeedAsync(RunDeliveryStatus.IntegratingTasks);
        var finalPr = await database.SeedAsync(RunDeliveryStatus.ReadyForFinalPullRequest);

        var results = await Task.WhenAll(database.ClaimReviewAsync("review"), database.ClaimFinalPrAsync("final-pr"));

        Assert.Equal([review.Id, finalPr.Id], results.Select(x => x!.Id).Order().ToArray());
    }

    [Fact]
    public async Task Active_lease_is_skipped_and_expired_lease_is_recovered()
    {
        if (!OperatingSystem.IsWindows())
            return;
        await using var database = await SqlDatabase.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        await database.SeedAsync(RunDeliveryStatus.IntegratingTasks, claimAt: now, claimExpiresAt: now.AddMinutes(2));

        Assert.Null(await database.ClaimReviewAsync("cannot-steal", now));
        var recovered = await database.ClaimReviewAsync("recovery", now.AddMinutes(3));

        Assert.NotNull(recovered);
        Assert.Equal("recovery", recovered.ClaimOwner);
    }

    [Fact]
    public async Task Terminal_delivery_is_never_claimed()
    {
        if (!OperatingSystem.IsWindows())
            return;
        await using var database = await SqlDatabase.CreateAsync();
        await database.SeedAsync(RunDeliveryStatus.Merged);

        Assert.Null(await database.ClaimReviewAsync("review"));
        Assert.Null(await database.ClaimFinalPrAsync("final-pr"));
    }

    [Fact]
    public async Task Stress_claimers_never_duplicate_delivery_ids()
    {
        if (!OperatingSystem.IsWindows())
            return;
        await using var database = await SqlDatabase.CreateAsync();
        for (var index = 0; index < 12; index++)
            await database.SeedAsync(RunDeliveryStatus.IntegratingTasks);

        var claimed = new List<Guid>();
        for (var round = 0; round < 12 && claimed.Count < 12; round++)
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 24).Select(index => database.ClaimReviewAsync($"stress-{round}-{index}")));
            claimed.AddRange(results.Where(x => x is not null).Select(x => x!.Id));
        }

        Assert.Equal(12, claimed.Count);
        Assert.Equal(claimed.Count, claimed.Distinct().Count());
    }

    private sealed class SqlDatabase(string connectionString) : IAsyncDisposable
    {
        public static async Task<SqlDatabase> CreateAsync()
        {
            var name = $"ImpersonateClaims_{Guid.NewGuid():N}";
            var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
            var database = new SqlDatabase(connection);
            await using var db = database.Context();
            await db.Database.EnsureCreatedAsync();
            return database;
        }

        public async Task<RunDelivery> SeedAsync(RunDeliveryStatus status, DateTimeOffset? claimAt = null, DateTimeOffset? claimExpiresAt = null)
        {
            await using var db = Context();
            var project = Project.Create($"Project-{Guid.NewGuid():N}", null, "https://github.com/owner/repo.git", "main");
            var run = PipelineRun.Create(project.Id, "claim concurrency test");
            var delivery = MoveTo(RunDelivery.Create(project.Id, run.Id, "main", "base", $"run/{Guid.NewGuid():N}"), status);
            if (claimAt is not null)
                delivery.Claim(Guid.NewGuid(), "existing", claimExpiresAt!.Value, claimAt.Value);
            db.Projects.Add(project);
            db.PipelineRuns.Add(run);
            db.RunDeliveries.Add(delivery);
            await db.SaveChangesAsync();
            return delivery;
        }

        public Task<RunDelivery?> ClaimReviewAsync(string owner, DateTimeOffset? at = null) => ClaimAsync(owner, at, true);
        public Task<RunDelivery?> ClaimFinalPrAsync(string owner, DateTimeOffset? at = null) => ClaimAsync(owner, at, false);

        private async Task<RunDelivery?> ClaimAsync(string owner, DateTimeOffset? value, bool review)
        {
            await using var db = Context();
            var repository = new EfRunDeliveryRepository(db, NullLogger<EfRunDeliveryRepository>.Instance);
            var at = value ?? DateTimeOffset.UtcNow;
            return review
                ? await repository.ClaimNextFinalReviewAsync(Guid.NewGuid(), owner, at, at.AddMinutes(1), default)
                : await repository.ClaimNextFinalPullRequestAsync(Guid.NewGuid(), owner, at, at.AddMinutes(1), default);
        }

        private ImpersonateDbContext Context() => new(new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlServer(connectionString).Options);

        public async ValueTask DisposeAsync()
        {
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
        }

        private static RunDelivery MoveTo(RunDelivery delivery, RunDeliveryStatus status)
        {
            if (status == RunDeliveryStatus.Pending)
                return delivery;
            delivery.StartPreparing();
            delivery.RecordRunBranch("head");
            delivery.StartTaskIntegration();
            if (status == RunDeliveryStatus.IntegratingTasks)
                return delivery;
            delivery.StartAggregateValidation();
            delivery.RecordAggregateValidation("[]");
            if (status == RunDeliveryStatus.FinalReview)
                return delivery;
            delivery.ApproveFinalReview(Guid.NewGuid(), "head");
            if (status == RunDeliveryStatus.ReadyForFinalPullRequest)
                return delivery;
            delivery.RecordFinalPullRequest("GitHubMCP:test", "owner/repo", 1, "https://github.com/owner/repo/pull/1", "head", "main");
            if (status == RunDeliveryStatus.FinalPullRequestOpen)
                return delivery;
            delivery.RecordMainReadiness("mergeable", "passed");
            delivery.RequestMerge();
            delivery.MarkMerged();
            return delivery;
        }
    }
}

public sealed class RunDeliveryClaimRetryPolicyTests
{
    [Fact]
    public async Task Transient_failure_retries_complete_operation_and_succeeds()
    {
        var attempts = 0;
        var warnings = 0;
        var result = await RunDeliveryClaimRetryPolicy.ExecuteAsync<int>(_ => ++attempts < 3 ? Task.FromException<int>(new TransientTestException()) : Task.FromResult(42), ex => ex is TransientTestException, _ => warnings++, default, delay: _ => TimeSpan.Zero);
        Assert.Equal(42, result);
        Assert.Equal(3, attempts);
        Assert.Equal(2, warnings);
    }

    [Fact]
    public async Task Retry_exhaustion_returns_safe_transient_failure()
    {
        var error = await Assert.ThrowsAsync<RunDeliveryClaimTransientException>(() => RunDeliveryClaimRetryPolicy.ExecuteAsync<int>(_ => Task.FromException<int>(new TransientTestException()), ex => ex is TransientTestException, _ => { }, default, delay: _ => TimeSpan.Zero));
        Assert.Equal("run_delivery_claim_transient_failure", error.Message);
    }

    [Fact]
    public async Task Cancellation_interrupts_retry_delay_and_domain_errors_are_not_retried()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(20);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunDeliveryClaimRetryPolicy.ExecuteAsync<int>(_ => Task.FromException<int>(new TransientTestException()), ex => ex is TransientTestException, _ => { }, cancellation.Token, delay: _ => TimeSpan.FromSeconds(5)));

        var attempts = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunDeliveryClaimRetryPolicy.ExecuteAsync<int>(_ => { attempts++; return Task.FromException<int>(new InvalidOperationException("domain")); }, ex => ex is TransientTestException, _ => { }, default));
        Assert.Equal(1, attempts);
    }

    private sealed class TransientTestException : Exception;
}
