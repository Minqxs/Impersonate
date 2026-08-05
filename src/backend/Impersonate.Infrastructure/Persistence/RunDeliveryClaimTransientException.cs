namespace Impersonate.Infrastructure.Persistence;

internal sealed class RunDeliveryClaimTransientException(string code) : Exception(code);
