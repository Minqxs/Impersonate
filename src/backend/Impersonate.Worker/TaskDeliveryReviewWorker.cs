using Impersonate.Application.Delivery;

namespace Impersonate.Worker;

public sealed class TaskDeliveryReviewWorker(IServiceScopeFactory scopes, ILogger<TaskDeliveryReviewWorker> logger) : BackgroundService
{
    private readonly string workerId = $"{Environment.MachineName}:{Environment.ProcessId}:delivery-review:{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ITaskDeliveryReviewer>().ProcessOneAsync(workerId, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Task delivery review polling cycle failed."); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }
}
