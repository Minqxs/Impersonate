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
}
