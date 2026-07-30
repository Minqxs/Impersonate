using Impersonate.Application.Pipelines;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;
using Impersonate.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class InfrastructureAttemptRollbackTests
{
    [Fact]
    public async Task Initial_infrastructure_rollback_persists_without_conceptual_null()
    {
        await using var fixture = await SqlFixture.CreateAsync();
        var run = await fixture.AddExecutingRunAsync();
        var repository = fixture.Repository;
        var now = DateTimeOffset.UtcNow;

        var claimed = await fixture.ClaimAsync(run, "worker-a", now, now.AddMinutes(5));
        var task = Assert.Single(claimed.Tasks);
        var transientAttempt = Assert.Single(task.Attempts);
        Assert.Equal(TaskAttemptStatus.Started, transientAttempt.Status);

        var rollback = claimed.BlockForInfrastructure(task, "workspace_preparation_failed", "Workspace preparation failed.", now.AddSeconds(1));
        repository.RemoveTransientAttempt(rollback.TransientAttempt);
        await repository.SaveChangesAsync(default);

        fixture.Database.ChangeTracker.Clear();
        var persisted = await fixture.Database.PipelineRuns
            .Include(x => x.Tasks).ThenInclude(x => x.Attempts)
            .SingleAsync(x => x.Id == run.Id);
        Assert.Equal(PipelineRunStatus.WaitingForInfrastructure, persisted.Status);
        Assert.Empty(Assert.Single(persisted.Tasks).Attempts);
    }

    [Fact]
    public async Task Infrastructure_failure_does_not_prevent_the_next_run_from_being_claimed()
    {
        await using var fixture = await SqlFixture.CreateAsync();
        var blockedRun = await fixture.AddExecutingRunAsync("Blocked run");
        var nextRun = await fixture.AddExecutingRunAsync("Next run");
        var repository = fixture.Repository;
        var now = DateTimeOffset.UtcNow;

        var claimedBlocked = await fixture.ClaimAsync(blockedRun, "worker-a", now, now.AddMinutes(5));
        Assert.Equal(blockedRun.Id, claimedBlocked.Id);
        var blockedTask = Assert.Single(claimedBlocked.Tasks);
        var rollback = claimedBlocked.BlockForInfrastructure(blockedTask, "workspace_preparation_failed", "Workspace preparation failed.", now.AddSeconds(1));
        repository.RemoveTransientAttempt(rollback.TransientAttempt);
        await repository.SaveChangesAsync(default);

        fixture.Database.ChangeTracker.Clear();
        var claimedNext = await fixture.ClaimAsync(nextRun, "worker-a", now.AddSeconds(2), now.AddMinutes(6));

        Assert.Equal(nextRun.Id, claimedNext.Id);
        Assert.Single(Assert.Single(claimedNext.Tasks).Attempts);
        fixture.Database.ChangeTracker.Clear();
        var persistedBlocked = await repository.GetAsync(blockedRun.ProjectId, blockedRun.Id, default);
        Assert.Equal(PipelineRunStatus.WaitingForInfrastructure, persistedBlocked!.Status);
        Assert.Null(persistedBlocked.ExecutionClaimId);
        Assert.Empty(Assert.Single(persistedBlocked.Tasks).Attempts);
    }

    [Fact]
    public async Task Ready_for_delivery_run_is_never_reclaimed()
    {
        await using var fixture = await SqlFixture.CreateAsync();
        var repository = fixture.Repository;
        var now = DateTimeOffset.UtcNow;
        var run = await fixture.AddExecutingRunAsync();
        var claimed = await fixture.ClaimAsync(run, "worker-a", now, now.AddMinutes(5));
        var task = Assert.Single(claimed.Tasks);
        task.CompleteAttempt("implemented");
        claimed.MoveTaskToReview(task, now.AddSeconds(1));
        claimed.RecordReview(task, ReviewDecisionType.Approved, "approved", at: now.AddSeconds(2));
        claimed.FinishApprovedTask(task, now.AddSeconds(3));
        await repository.SaveChangesAsync(default);
        fixture.Database.ChangeTracker.Clear();

        Assert.False(await fixture.Database.PipelineRuns.AnyAsync(x => x.Status == PipelineRunStatus.Executing));
        var persisted = await repository.GetAsync(run.ProjectId, run.Id, default);
        Assert.Equal(PipelineRunStatus.ReadyForDelivery, persisted!.Status);
        Assert.Equal(LoopStage.Committing, persisted.LoopRun.CurrentStage);
    }

    [Fact]
    public async Task Revision_rollback_deletes_only_the_new_attempt_and_retry_keeps_contiguous_numbering()
    {
        await using var fixture = await SqlFixture.CreateAsync();
        var run = await fixture.AddExecutingRunAsync();
        var repository = fixture.Repository;
        var now = DateTimeOffset.UtcNow;
        var initialRun = await fixture.ClaimAsync(run, "worker-a", now, now.AddMinutes(5));
        var task = Assert.Single(initialRun.Tasks);
        var initialAttemptId = Assert.Single(task.Attempts).Id;
        task.CompleteAttempt("implemented");
        initialRun.MoveTaskToReview(task, now.AddSeconds(1));
        var review = initialRun.RecordReview(task, ReviewDecisionType.ChangesRequested, "needs work", "Fix validation", now.AddSeconds(2));
        initialRun.ClearExecutionClaim();
        await repository.SaveChangesAsync(default);
        fixture.Database.ChangeTracker.Clear();

        var revisionRun = await fixture.ClaimAsync(run, "worker-b", now.AddSeconds(3), now.AddMinutes(6));
        var revisionTask = Assert.Single(revisionRun.Tasks);
        var transient = revisionTask.Attempts.Single(x => x.AttemptNumber == 2);
        var rollback = revisionRun.BlockForInfrastructure(revisionTask, "workspace_preparation_failed", "Workspace preparation failed.", now.AddSeconds(4));
        repository.RemoveTransientAttempt(rollback.TransientAttempt);
        await repository.SaveChangesAsync(default);
        fixture.Database.ChangeTracker.Clear();

        var persisted = await repository.GetAsync(run.ProjectId, run.Id, default);
        var persistedTask = Assert.Single(persisted!.Tasks);
        Assert.Equal(PlannedTaskStatus.ChangesRequested, persistedTask.Status);
        Assert.Equal(0, persistedTask.RevisionCount);
        Assert.Equal(initialAttemptId, Assert.Single(persistedTask.Attempts).Id);
        Assert.Equal(review.Id, Assert.Single(persistedTask.ReviewDecisions).Id);
        Assert.DoesNotContain(persistedTask.Attempts, x => x.Id == transient.Id);

        persisted.RetryInfrastructure(now.AddSeconds(5));
        await repository.SaveChangesAsync(default);
        fixture.Database.ChangeTracker.Clear();
        var retried = await fixture.ClaimAsync(run, "worker-c", now.AddSeconds(6), now.AddMinutes(7));
        Assert.Equal(2, Assert.Single(retried.Tasks).Attempts.Last().AttemptNumber);
        Assert.Equal(1, Assert.Single(retried.Tasks).RevisionCount);
    }

    private sealed class SqlFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private SqlFixture(ImpersonateDbContext database, SqliteConnection connection)
        {
            this.connection = connection;
            Database = database;
            Repository = new EfPipelineRunRepository(database);
        }

        public ImpersonateDbContext Database
        {
            get;
        }
        public IPipelineRunRepository Repository
        {
            get;
        }

        public static async Task<SqlFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlite(connection).Options;
            var fixture = new SqlFixture(new ImpersonateDbContext(options), connection);
            await fixture.Database.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async Task<PipelineRun> AddExecutingRunAsync(string name = "Rollback fixture")
        {
            var project = Project.Create(name, null, $"https://github.com/example/{Guid.NewGuid():N}", "main");
            var run = PipelineRun.Create(project.Id, "Exercise infrastructure rollback");
            run.StartPlanning();
            run.AddTask(1, "Prepare workspace", "Prepare the task workspace", ["Workspace is prepared"]);
            run.MarkReadyForExecution();
            run.StartExecution();
            Database.Projects.Add(project);
            Database.PipelineRuns.Add(run);
            await Database.SaveChangesAsync();
            Database.ChangeTracker.Clear();
            return run;
        }

        public async Task<PipelineRun> ClaimAsync(PipelineRun identity, string workerId, DateTimeOffset claimedAt, DateTimeOffset expiresAt)
        {
            var run = await Repository.GetAsync(identity.ProjectId, identity.Id, default) ?? throw new InvalidOperationException("Run not found.");
            run.ClaimNextTask(Guid.NewGuid(), workerId, expiresAt, claimedAt);
            await Repository.SaveChangesAsync(default);
            return run;
        }

        public async ValueTask DisposeAsync()
        {
            await Database.Database.EnsureDeletedAsync();
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
