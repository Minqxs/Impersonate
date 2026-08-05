using System.Data;
using System.Data.Common;
using Impersonate.Application.Delivery;
using Impersonate.Domain.Delivery;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Impersonate.Infrastructure.Persistence;

internal sealed class EfRunDeliveryRepository(ImpersonateDbContext db, ILogger<EfRunDeliveryRepository> logger) : IRunDeliveryRepository
{
    private const int MaximumClaimAttempts = 3;
    public Task<RunDelivery?> GetByRunAsync(Guid projectId, Guid runId, CancellationToken ct) => db.RunDeliveries.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.PipelineRunId == runId, ct);
    public Task AddAsync(RunDelivery delivery, CancellationToken ct) => db.RunDeliveries.AddAsync(delivery, ct).AsTask();

    public Task<RunDelivery?> ClaimNextFinalReviewAsync(Guid claimId, string owner, DateTimeOffset at, DateTimeOffset expiresAt, CancellationToken ct) =>
        ClaimAsync(claimId, owner, at, expiresAt,
            [RunDeliveryStatus.IntegratingTasks, RunDeliveryStatus.AggregateValidation, RunDeliveryStatus.FinalReview, RunDeliveryStatus.ChangesRequested], ct);

    public Task<RunDelivery?> ClaimNextFinalPullRequestAsync(Guid claimId, string owner, DateTimeOffset at, DateTimeOffset expiresAt, CancellationToken ct) =>
        ClaimAsync(claimId, owner, at, expiresAt,
            [RunDeliveryStatus.ReadyForFinalPullRequest, RunDeliveryStatus.FinalPullRequestOpen], ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private async Task<RunDelivery?> ClaimAsync(Guid claimId, string owner, DateTimeOffset at, DateTimeOffset expiresAt, IReadOnlyList<RunDeliveryStatus> statuses, CancellationToken ct)
    {
        if (claimId == Guid.Empty || string.IsNullOrWhiteSpace(owner) || owner.Length > 200 || expiresAt <= at)
            throw new ArgumentException("A valid run delivery claim is required.");

        Guid? id;
        try
        {
            id = await RunDeliveryClaimRetryPolicy.ExecuteAsync(
                token => ExecuteAtomicClaimAsync(claimId, owner, at, expiresAt, statuses, token),
                ex => ex is SqlException sql && IsTransient(sql),
                attempt =>
                {
                    db.ChangeTracker.Clear();
                    logger.LogWarning("Transient SQL failure while claiming a run delivery; retrying claim attempt {Attempt} of {MaximumAttempts}.", attempt, MaximumClaimAttempts);
                }, ct, MaximumClaimAttempts);
        }
        catch (RunDeliveryClaimTransientException)
        {
            db.ChangeTracker.Clear();
            throw;
        }
        if (id is null)
            return null;

        var tracked = db.RunDeliveries.Local.SingleOrDefault(x => x.Id == id.Value);
        if (tracked is not null)
        {
            await db.Entry(tracked).ReloadAsync(ct);
            return tracked;
        }
        return await db.RunDeliveries.SingleAsync(x => x.Id == id.Value, ct);
    }

    private async Task<Guid?> ExecuteAtomicClaimAsync(Guid claimId, string owner, DateTimeOffset at, DateTimeOffset expiresAt, IReadOnlyList<RunDeliveryStatus> statuses, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close)
            await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                ;WITH candidate AS
                (
                    SELECT TOP (1) *
                    FROM [RunDeliveries] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK, ROWLOCK)
                    WHERE [ClaimId] = @claimId
                       OR ([Status] IN ({string.Join(", ", statuses.Select((_, index) => $"@status{index}"))})
                           AND ([ClaimExpiresAtUtc] IS NULL OR [ClaimExpiresAtUtc] <= @claimedAt))
                    ORDER BY CASE WHEN [ClaimId] = @claimId THEN 0 ELSE 1 END, [UpdatedAtUtc], [Id]
                )
                UPDATE candidate
                SET [ClaimId] = @claimId,
                    [ClaimOwner] = @owner,
                    [ClaimedAtUtc] = @claimedAt,
                    [ClaimExpiresAtUtc] = @expiresAt,
                    [UpdatedAtUtc] = @claimedAt
                OUTPUT INSERTED.[Id];
                """;
            Add(command, "@claimId", claimId);
            Add(command, "@owner", owner);
            Add(command, "@claimedAt", at);
            Add(command, "@expiresAt", expiresAt);
            for (var index = 0; index < statuses.Count; index++)
                Add(command, $"@status{index}", (int)statuses[index]);
            var result = await command.ExecuteScalarAsync(ct);
            return result is Guid id ? id : null;
        }
        finally
        {
            if (close)
                await connection.CloseAsync();
        }
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static bool IsTransient(SqlException exception) => exception.Errors.Cast<SqlError>().Any(x => x.Number is 1205 or -2 or 4060 or 10928 or 10929 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920);
}
