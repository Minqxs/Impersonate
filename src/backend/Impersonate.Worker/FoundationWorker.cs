namespace Impersonate.Worker;

/// <summary>Hosts future pipeline execution once a product module defines work to process.</summary>
public sealed class FoundationWorker(ILogger<FoundationWorker> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Impersonate worker started; no pipeline work is configured in the foundation milestone.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Impersonate worker is stopping.");
        return Task.CompletedTask;
    }
}
