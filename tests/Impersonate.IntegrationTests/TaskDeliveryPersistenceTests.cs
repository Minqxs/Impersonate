using Impersonate.Domain.Delivery;
using Impersonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class TaskDeliveryPersistenceTests
{
    [Fact]
    public void Task_delivery_model_has_required_uniqueness_and_restrictive_relationships()
    {
        var options = new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ImpersonateModelOnly;Trusted_Connection=True").Options;
        using var db = new ImpersonateDbContext(options);
        var entity = db.Model.FindEntityType(typeof(TaskDelivery))!;
        Assert.Equal("TaskDeliveries", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual([nameof(TaskDelivery.PlannedTaskId)]));
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual([nameof(TaskDelivery.IdempotencyKey)]));
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Select(x => x.Name).SequenceEqual([nameof(TaskDelivery.PipelineRunId), nameof(TaskDelivery.Status)]));
        Assert.Contains(entity.GetForeignKeys(), foreignKey => foreignKey.Properties.Any(x => x.Name == nameof(TaskDelivery.PlannedTaskId)) && foreignKey.DeleteBehavior == DeleteBehavior.NoAction);
    }

    [Fact]
    public void Task_delivery_reviews_persist_exact_head_attempt_identity()
    {
        var options = new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ImpersonateModelOnly;Trusted_Connection=True").Options;
        using var db = new ImpersonateDbContext(options);
        var entity = db.Model.FindEntityType(typeof(TaskDeliveryReview))!;
        Assert.Equal("TaskDeliveryReviews", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual([nameof(TaskDeliveryReview.TaskDeliveryId), nameof(TaskDeliveryReview.ReviewAttemptNumber)]));
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Select(x => x.Name).SequenceEqual([nameof(TaskDeliveryReview.TaskDeliveryId), nameof(TaskDeliveryReview.ExactHeadSha)]));
        Assert.Contains(entity.GetForeignKeys(), foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(TaskDelivery) && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
    }

    [Fact]
    public void Run_delivery_reviews_persist_exact_head_attempt_identity()
    {
        var options = new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ImpersonateModelOnly;Trusted_Connection=True").Options;
        using var db = new ImpersonateDbContext(options);
        var entity = db.Model.FindEntityType(typeof(RunDeliveryReview))!;
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual([nameof(RunDeliveryReview.RunDeliveryId), nameof(RunDeliveryReview.AttemptNumber)]));
        Assert.Contains(entity.GetForeignKeys(), foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(RunDelivery) && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
    }
}
