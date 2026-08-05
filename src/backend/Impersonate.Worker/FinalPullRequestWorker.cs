using Impersonate.Application.Delivery;

namespace Impersonate.Worker;

public sealed class FinalPullRequestWorker(IServiceScopeFactory scopes, ILogger<FinalPullRequestWorker> logger) : BackgroundService
{
    private readonly string workerId = $"{Environment.MachineName}:{Environment.ProcessId}:final-pr:{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IFinalPullRequestOrchestrator>().ProcessOneAsync(workerId, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Final pull-request polling cycle failed."); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }
}
