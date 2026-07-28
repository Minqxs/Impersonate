using Impersonate.Domain.Pipelines;
using Impersonate.Application.Pipelines;
using System.Reflection;
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

    [Fact]
    public void Adding_a_task_to_a_tracked_run_marks_the_task_as_added()
    {
        var options=new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MappingOnly;Trusted_Connection=True").Options;
        using var db=new ImpersonateDbContext(options);var run=PipelineRun.Create(Guid.NewGuid(),"Plan a feature");run.StartPlanning();db.Attach(run);run.AddTask(1,"Implement feature","Implement the requested feature.");db.ChangeTracker.DetectChanges();
        var task=db.ChangeTracker.Entries<PlannedTask>().Single();
        Assert.Equal(EntityState.Added,task.State);
    }

    [Fact]
    public void Starting_coding_on_a_tracked_run_marks_the_application_generated_attempt_as_added()
    {
        var options=new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MappingOnly;Trusted_Connection=True").Options;
        using var db=new ImpersonateDbContext(options);var run=PipelineRun.Create(Guid.NewGuid(),"Implement a feature");run.StartPlanning();run.AddTask(1,"Implement","Do the work");run.MarkReadyForExecution();run.StartExecution();db.Attach(run);var now=DateTimeOffset.UtcNow;var task=run.ClaimNextTask(Guid.NewGuid(),"worker",now.AddMinutes(5),now);db.ChangeTracker.DetectChanges();
        var attempt=db.ChangeTracker.Entries<TaskAttempt>().Single();
        Assert.Equal(EntityState.Added,attempt.State);Assert.NotEqual(Guid.Empty,attempt.Entity.Id);Assert.Equal(task.Id,attempt.Entity.PlannedTaskId);
    }

    [Fact]
    public void Reviewing_a_tracked_attempt_marks_the_application_generated_decision_as_added()
    {
        var options=new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MappingOnly;Trusted_Connection=True").Options;
        using var db=new ImpersonateDbContext(options);var run=PipelineRun.Create(Guid.NewGuid(),"Implement a feature");run.StartPlanning();run.AddTask(1,"Implement","Do the work");run.MarkReadyForExecution();run.StartExecution();var now=DateTimeOffset.UtcNow;var task=run.ClaimNextTask(Guid.NewGuid(),"worker",now.AddMinutes(5),now);task.CompleteAttempt("done");run.MoveTaskToReview(task);db.Attach(run);run.RecordReview(task,ReviewDecisionType.Approved,"approved");db.ChangeTracker.DetectChanges();
        var review=db.ChangeTracker.Entries<ReviewDecision>().Single();
        Assert.Equal(EntityState.Added,review.State);Assert.NotEqual(Guid.Empty,review.Entity.Id);
    }

    [Fact]
    public void Execution_invocation_uses_an_application_generated_identifier()
    {
        var options=new DbContextOptionsBuilder<ImpersonateDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MappingOnly;Trusted_Connection=True").Options;
        using var db=new ImpersonateDbContext(options);var invocation=ExecutionInvocation.Record(Guid.NewGuid(),1,"Coder","OpenAI","gpt-4.1",null,"coder-v1","request",10,5,"complete",0,1,0,0,0,false,"coder_protocol_failed","Premature completion.",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);db.ExecutionInvocations.Add(invocation);db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Added,db.Entry(invocation).State);Assert.NotEqual(Guid.Empty,invocation.Id);Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never,db.Model.FindEntityType(typeof(ExecutionInvocation))!.FindProperty(nameof(ExecutionInvocation.Id))!.ValueGenerated);
    }

    [Fact]
    public void Pipeline_projection_includes_incremental_composition_telemetry()
    {
        var run=PipelineRun.Create(Guid.NewGuid(),"Implement dependent task");run.StartPlanning();run.AddTask(1,"Dependency","Approved dependency");var dependent=run.AddTask(2,"Dependent","Uses dependency");run.MarkReadyForExecution();run.StartTaskExecution(dependent);var now=DateTimeOffset.UtcNow;var task=run.ClaimNextTask(Guid.NewGuid(),"worker",now.AddMinutes(5),now);var attempt=task.Attempts.Single();var dependencyId=Guid.NewGuid();attempt.RecordComposition(new string('a',40),[dependencyId],new string('b',40),true);attempt.RecordExecution("OpenAI","gpt-test","coder-v1","request",10,5,2,"[\"One.cs\",\"Two.cs\"]","artifact:patch",new string('c',64),"[]");
        var serviceType=typeof(PipelineRunDto).Assembly.GetType("Impersonate.Application.Pipelines.PipelineRunService")!;var map=serviceType.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(x=>x.Name=="Map"&&x.GetParameters().Length==3);var dto=(PipelineRunDto)map.Invoke(null,[run,null,null])!;var projected=dto.Tasks.Single(x=>x.Id==task.Id).Attempts.Single();
        Assert.Equal(new string('a',40),projected.SourceBaseCommitSha);Assert.Equal(1,projected.DependencyPatchCount);Assert.Equal([dependencyId],projected.DependencyTaskIds);Assert.Equal(new string('b',40),projected.ComposedTreeFingerprint);Assert.True(projected.CurrentRevisionPatchApplied);Assert.Equal(2,projected.IncrementalPatchFileCount);Assert.Equal("Composed",projected.CompositionStatus);
    }
}
