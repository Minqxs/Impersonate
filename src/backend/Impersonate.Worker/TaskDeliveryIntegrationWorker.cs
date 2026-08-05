using Impersonate.Application.Delivery;

namespace Impersonate.Worker;

public sealed class TaskDeliveryIntegrationWorker(IServiceScopeFactory scopes, ILogger<TaskDeliveryIntegrationWorker> logger) : BackgroundService
{
    private readonly string workerId = $"{Environment.MachineName}:{Environment.ProcessId}:delivery-integration:{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ITaskDeliveryIntegrator>().ProcessOneAsync(workerId, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Task delivery integration polling cycle failed."); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }
}
