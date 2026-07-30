namespace Impersonate.Application.Quality;

public sealed record CodeQualityProviderResult(
    bool Succeeded, ProjectQualitySummary? Summary, string? FailureCode,
    string? SafeMessage, ProjectQualityState FailureState)
{
    public static CodeQualityProviderResult Ok(ProjectQualitySummary summary) =>
        new(true, summary, null, null, summary.State);

    public static CodeQualityProviderResult Fail(string code, string message, ProjectQualityState state) =>
        new(false, null, code, message, state);
}
