using Impersonate.Application.Execution;

namespace Impersonate.Worker;

public sealed class TaskExecutionWorker(IServiceScopeFactory scopes, IExecutionEnvironmentReadinessService readiness, ILogger<TaskExecutionWorker> logger) : BackgroundService
{
    private readonly string workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var environment = await readiness.CheckAsync(stoppingToken);
        logger.LogInformation("Task execution worker environment readiness {Status} on {OperatingSystem}; sanitized variables: {VariableNames}.", environment.Ready ? "Ready" : "Blocked", environment.OperatingSystem, environment.SuppliedVariableNames);
        if (!environment.Ready)
            logger.LogWarning("Task execution environment is blocked: {Blockers}", environment.Blockers);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processed = await scope.ServiceProvider.GetRequiredService<ITaskExecutionOrchestrator>().ProcessOneAsync(workerId, stoppingToken);
                if (!processed)
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (TaskExecutionPersistenceException ex)
            {
                logger.LogError(
                    "Task execution polling cycle failed for pipeline {PipelineRunId} and task {PlannedTaskId} during persistence ({ExceptionType}).",
                    ex.PipelineRunId, ex.PlannedTaskId, ex.ExceptionType);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex) { logger.LogError(ex, "Task execution polling cycle failed."); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }
}
