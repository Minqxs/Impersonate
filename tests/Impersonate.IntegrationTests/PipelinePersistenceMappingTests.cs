using Impersonate.Domain.Pipelines;
using Impersonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class PipelinePersistenceMappingTests
{
    [Fact]
    public void Starting_a_tracked_run_marks_the_new_audit_event_as_added()
    {
        var options=new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MappingOnly;Trusted_Connection=True").Options;
        using var db=new ImpersonateDbContext(options);var run=PipelineRun.Create(Guid.NewGuid(),"Plan a feature");db.Attach(run);run.StartPlanning();db.ChangeTracker.DetectChanges();
        var planningStarted=db.ChangeTracker.Entries<PipelineRunEvent>().Single(x=>x.Entity.EventType=="PlanningStarted");
        Assert.Equal(EntityState.Added,planningStarted.State);
    }
}
