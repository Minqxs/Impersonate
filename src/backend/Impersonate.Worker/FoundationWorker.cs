using Impersonate.Application.Planning;
using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Impersonate.Domain.Ai;

namespace Impersonate.Worker;
public sealed class FoundationWorker(IServiceScopeFactory scopes,IOptions<PlannerOptions> options,ILogger<FoundationWorker> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
  logger.LogInformation("Planner worker started with persisted model routing and environment fallback enabled.");
  using var timer=new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds));
  do { try{await ProcessOneAsync(stoppingToken);}catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){}catch(Exception ex){logger.LogError(ex,"Planner polling cycle failed.");} } while(await timer.WaitForNextTickAsync(stoppingToken));
 }
 private async Task ProcessOneAsync(CancellationToken ct)
 {
  using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ImpersonateDbContext>();var agent=scope.ServiceProvider.GetRequiredService<IPlannerAgent>();var now=DateTimeOffset.UtcNow;
  await using var claimTx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
  var run=await db.PipelineRuns.Include(x=>x.LoopRun).Include(x=>x.Tasks).Include(x=>x.Events).Where(x=>x.Status==PipelineRunStatus.Planning&&(x.PlanningClaimExpiresAtUtc==null||x.PlanningClaimExpiresAtUtc<now)).OrderBy(x=>x.CreatedAtUtc).FirstOrDefaultAsync(ct);
  if(run is null){await claimTx.RollbackAsync(ct);return;}
  var claim=Guid.NewGuid();var lease=TimeSpan.FromSeconds((options.Value.TimeoutSeconds+30)*options.Value.MaximumPlanningAttempts);run.ClaimPlanning(claim,Environment.MachineName,now.Add(lease),now);await db.SaveChangesAsync(ct);await claimTx.CommitAsync(ct);
  var project=await db.Projects.SingleAsync(x=>x.Id==run.ProjectId,ct);var decision=await db.ModelSelectionDecisions.OrderByDescending(x=>x.CreatedAtUtc).FirstAsync(x=>x.ProjectId==run.ProjectId&&x.PipelineRunId==run.Id&&x.Role==AgentRole.Planner,ct);var prior=await db.PlanningAttempts.CountAsync(x=>x.PipelineRunId==run.Id,ct);string? correction=null;
  for(var number=prior+1;number<=options.Value.MaximumPlanningAttempts;number++)
  {
   var attempt=PlanningAttempt.Start(run.Id,number,decision.Provider,decision.Model,options.Value.PromptVersion);db.PlanningAttempts.Add(attempt);await db.SaveChangesAsync(ct);
   try
   {
    ProviderType? routedProvider=decision.ProviderConnectionId is null?null:Enum.Parse<ProviderType>(decision.Provider);var result=await agent.PlanAsync(new(project.Id,project.Name,project.Description,project.RepositoryUrl,project.DefaultBranch,run.FeatureRequest,options.Value.MaximumTasks,options.Value.PromptVersion,correction,decision.ProviderConnectionId,routedProvider,decision.Model),ct);var errors=PlannerPlanValidator.Validate(result.Plan,options.Value.MaximumTasks);
    if(errors.Count>0){correction=string.Join(" ",errors).Substring(0,Math.Min(1000,string.Join(" ",errors).Length));attempt.Fail(PlanningAttemptStatus.InvalidOutput,"invalid_output",correction,result.ProviderRequestId);await db.SaveChangesAsync(ct);continue;}
    await using var successTx=await db.Database.BeginTransactionAsync(ct);
    if(!result.Plan.CanPlan)run.RequireClarification(result.Plan.FailureReason!,result.Plan.ClarifyingQuestion!);else{foreach(var task in result.Plan.Tasks.OrderBy(x=>x.Sequence))run.AddTask(task.Sequence,task.Title,task.Description,task.AcceptanceCriteria);run.MarkReadyForExecution();}
    attempt.Succeed(result.ProviderRequestId,result.InputTokenCount,result.OutputTokenCount);await db.SaveChangesAsync(ct);await successTx.CommitAsync(ct);logger.LogInformation("Planning completed for project {ProjectId}, pipeline {PipelineId}, attempt {Attempt}.",project.Id,run.Id,number);return;
   }
   catch(ProviderCredentialUnavailableException ex){attempt.Fail(PlanningAttemptStatus.ProviderFailed,ex.Code,ex.Message);run.Fail(ex.Message);run.ClearPlanningClaim();await db.SaveChangesAsync(ct);logger.LogWarning("Planning stopped for project {ProjectId}, pipeline {PipelineId}: provider credential configuration {FailureCode}.",project.Id,run.Id,ex.Code);return;}
   catch(ProviderRequestException ex){attempt.Fail(PlanningAttemptStatus.ProviderFailed,ex.Code,ex.Message);await db.SaveChangesAsync(ct);logger.LogWarning(ex,"Planning attempt {Attempt} failed for project {ProjectId}, pipeline {PipelineId}: provider returned HTTP {StatusCode} ({FailureCode}).",number,project.Id,run.Id,(int)ex.StatusCode,ex.Code);if(ex.IsTransient)continue;run.Fail(ex.Message);run.ClearPlanningClaim();await db.SaveChangesAsync(ct);return;}
   catch(OperationCanceledException) when(ct.IsCancellationRequested){attempt.Fail(PlanningAttemptStatus.Cancelled,"cancelled","Planning was cancelled.");await db.SaveChangesAsync(CancellationToken.None);throw;}
   catch(Exception ex){var timedOut=ex is TaskCanceledException;attempt.Fail(timedOut?PlanningAttemptStatus.TimedOut:PlanningAttemptStatus.ProviderFailed,timedOut?"provider_timeout":"provider_failed",timedOut?"The configured planner provider timed out.":$"The planner failed while processing the provider response ({ex.GetType().Name}).");await db.SaveChangesAsync(ct);logger.LogWarning(ex,"Planning attempt {Attempt} failed for project {ProjectId}, pipeline {PipelineId} ({FailureType}).",number,project.Id,run.Id,ex.GetType().Name);}
  }
  run.Fail("Planning attempts were exhausted.");run.ClearPlanningClaim();await db.SaveChangesAsync(ct);
 }
}
