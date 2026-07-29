using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlannerReadiness(string Status, bool ProviderConfigured, bool ModelConfigured, bool CredentialsConfigured, string Message)
{
    public bool IsReady => Status == "Ready";
}
