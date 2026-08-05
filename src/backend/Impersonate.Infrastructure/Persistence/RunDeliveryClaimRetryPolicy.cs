namespace Impersonate.Infrastructure.Persistence;

internal static class RunDeliveryClaimRetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, Func<Exception, bool> transient, Action<int> onRetry, CancellationToken ct, int maximumAttempts = 3, Func<int, TimeSpan>? delay = null)
    {
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await operation(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (transient(ex) && attempt < maximumAttempts)
            {
                onRetry(attempt);
                var pause = delay?.Invoke(attempt) ?? TimeSpan.FromMilliseconds(20 * attempt + Random.Shared.Next(10, 41));
                await Task.Delay(pause, ct);
            }
            catch (Exception ex) when (transient(ex))
            {
                throw new RunDeliveryClaimTransientException("run_delivery_claim_transient_failure");
            }
        }
        throw new InvalidOperationException("Run delivery claim retry policy reached an invalid state.");
    }
}
