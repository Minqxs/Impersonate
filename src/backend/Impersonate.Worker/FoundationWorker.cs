using Impersonate.Application.Planning;
using Impersonate.Domain.Pipelines;
using Impersonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Impersonate.Worker;
public sealed class FoundationWorker(IServiceScopeFactory scopes,IOptions<PlannerOptions> options,ILogger<FoundationWorker> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
  logger.LogInformation("Planner worker started.");
  using var timer=new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds));
  do { try{await ProcessOneAsync(stoppingToken);}catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){}catch(Exception ex){logger.LogError(ex,"Planner polling cycle failed.");} } while(await timer.WaitForNextTickAsync(stoppingToken));
 }
 private async Task ProcessOneAsync(CancellationToken ct)
 {
  using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ImpersonateDbContext>();var agent=scope.ServiceProvider.GetRequiredService<IPlannerAgent>();var now=DateTimeOffset.UtcNow;
  await using var claimTx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
  var run=await db.PipelineRuns.Include(x=>x.LoopRun).Include(x=>x.Tasks).Include(x=>x.Events).Where(x=>x.Status==PipelineRunStatus.Planning&&(x.PlanningClaimExpiresAtUtc==null||x.PlanningClaimExpiresAtUtc<now)).OrderBy(x=>x.CreatedAtUtc).FirstOrDefaultAsync(ct);
  if(run is null){await claimTx.RollbackAsync(ct);return;}
  var claim=Guid.NewGuid();run.ClaimPlanning(claim,Environment.MachineName,now.AddMinutes(5),now);await db.SaveChangesAsync(ct);await claimTx.CommitAsync(ct);
  var project=await db.Projects.SingleAsync(x=>x.Id==run.ProjectId,ct);var prior=await db.PlanningAttempts.CountAsync(x=>x.PipelineRunId==run.Id,ct);string? correction=null;
  for(var number=prior+1;number<=options.Value.MaximumPlanningAttempts;number++)
  {
   var attempt=PlanningAttempt.Start(run.Id,number,options.Value.Provider,options.Value.Model,options.Value.PromptVersion);db.PlanningAttempts.Add(attempt);await db.SaveChangesAsync(ct);
   try
   {
    var result=await agent.PlanAsync(new(project.Id,project.Name,project.Description,project.RepositoryUrl,project.DefaultBranch,run.FeatureRequest,options.Value.MaximumTasks,options.Value.PromptVersion,correction),ct);var errors=PlannerPlanValidator.Validate(result.Plan,options.Value.MaximumTasks);
    if(errors.Count>0){correction=string.Join(" ",errors).Substring(0,Math.Min(1000,string.Join(" ",errors).Length));attempt.Fail(PlanningAttemptStatus.InvalidOutput,"invalid_output",correction,result.ProviderRequestId);await db.SaveChangesAsync(ct);continue;}
    await using var successTx=await db.Database.BeginTransactionAsync(ct);
    if(!result.Plan.CanPlan)run.RequireClarification(result.Plan.FailureReason!,result.Plan.ClarifyingQuestion!);else{foreach(var task in result.Plan.Tasks.OrderBy(x=>x.Sequence))run.AddTask(task.Sequence,task.Title,task.Description,task.AcceptanceCriteria);run.MarkReadyForExecution();}
    attempt.Succeed(result.ProviderRequestId,result.InputTokenCount,result.OutputTokenCount);await db.SaveChangesAsync(ct);await successTx.CommitAsync(ct);logger.LogInformation("Planning completed for project {ProjectId}, pipeline {PipelineId}, attempt {Attempt}.",project.Id,run.Id,number);return;
   }
   catch(OperationCanceledException) when(ct.IsCancellationRequested){attempt.Fail(PlanningAttemptStatus.Cancelled,"cancelled","Planning was cancelled.");await db.SaveChangesAsync(CancellationToken.None);throw;}
   catch(Exception ex){attempt.Fail(ex is TaskCanceledException?PlanningAttemptStatus.TimedOut:PlanningAttemptStatus.ProviderFailed,"provider_failed","The configured planner provider failed.");await db.SaveChangesAsync(ct);logger.LogWarning(ex,"Planning attempt {Attempt} failed for pipeline {PipelineId}.",number,run.Id);}
  }
  run.Fail("Planning attempts were exhausted.");run.ClearPlanningClaim();await db.SaveChangesAsync(ct);
 }
}
