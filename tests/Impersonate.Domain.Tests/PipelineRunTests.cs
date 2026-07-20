using Impersonate.Domain.Pipelines;
using Xunit;
namespace Impersonate.Domain.Tests;
public sealed class PipelineRunTests
{
 [Fact] public void Creation_snapshots_policy_and_records_event(){var r=PipelineRun.Create(Guid.NewGuid(),"Deliver health endpoint",3,true);Assert.Equal(PipelineRunStatus.Created,r.Status);Assert.Equal(3,r.LoopRun.MaximumRevisionAttempts);Assert.Equal("PipelineCreated",Assert.Single(r.Events).EventType);}
 [Fact] public void Straight_approval_requires_review_before_commit(){var r=PipelineRun.Create(Guid.NewGuid(),"Feature");r.StartPlanning();var t=r.AddTask(1,"Implement","Do work");r.StartExecution();t.StartCoding();Assert.Throws<InvalidOperationException>(()=>t.StartCommit());t.CompleteAttempt("done");t.SubmitForReview();t.Review(ReviewDecisionType.Approved,"good");t.StartCommit();t.MarkCommitted();r.Complete();Assert.Equal(PipelineRunStatus.Completed,r.Status);}
 [Fact] public void Revision_is_capped_and_exhausted_task_can_be_skipped(){var r=PipelineRun.Create(Guid.NewGuid(),"Feature",1,true);r.StartPlanning();var t=r.AddTask(1,"Implement","Do work");r.AddTask(2,"Continue","Remaining work");r.StartExecution();t.StartCoding();t.CompleteAttempt("v1");t.SubmitForReview();t.Review(ReviewDecisionType.ChangesRequested,"revise","fix it");t.StartRevision();t.CompleteAttempt("v2");t.SubmitForReview();t.Review(ReviewDecisionType.ChangesRequested,"still wrong","fix again");Assert.Throws<InvalidOperationException>(()=>t.StartRevision());t.Skip("Retry limit reached.");Assert.Equal(PlannedTaskStatus.Pending,r.Tasks[1].Status);}
 [Fact] public void Terminal_pipeline_cannot_reopen(){var r=PipelineRun.Create(Guid.NewGuid(),"Feature");r.Cancel();Assert.Throws<InvalidOperationException>(()=>r.StartPlanning());Assert.Throws<InvalidOperationException>(()=>r.Cancel());}
 [Fact] public void Duplicate_sequence_is_rejected(){var r=PipelineRun.Create(Guid.NewGuid(),"Feature");r.StartPlanning();r.AddTask(1,"One","First");Assert.Throws<InvalidOperationException>(()=>r.AddTask(1,"Two","Second"));}
}
