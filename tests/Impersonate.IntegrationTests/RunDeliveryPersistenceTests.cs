using Impersonate.Domain.Delivery;
using Impersonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class RunDeliveryPersistenceTests
{
    [Fact]
    public void Run_delivery_model_enforces_one_aggregate_and_one_run_branch_per_run()
    {
        var options = new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ImpersonateModelOnly;Trusted_Connection=True").Options;
        using var db = new ImpersonateDbContext(options);
        var entity = db.Model.FindEntityType(typeof(RunDelivery))!;
        Assert.Equal("RunDeliveries", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual([nameof(RunDelivery.PipelineRunId)]));
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual([nameof(RunDelivery.ProjectId), nameof(RunDelivery.RunBranchName)]));
        Assert.DoesNotContain(entity.GetProperties(), x => x.Name.Contains("Patch", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Credential", StringComparison.OrdinalIgnoreCase));
    }
}
