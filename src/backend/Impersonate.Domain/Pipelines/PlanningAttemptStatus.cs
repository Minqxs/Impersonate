namespace Impersonate.Domain.Pipelines;

public enum PlanningAttemptStatus
{
    Started,
    Succeeded,
    InvalidOutput,
    ProviderFailed,
    TimedOut,
    Cancelled
}
