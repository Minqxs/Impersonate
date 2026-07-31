namespace Impersonate.Application.Quality;

public interface ICodeQualityProvider
{
    Task<CodeQualityProviderResult> GetSummaryAsync(
        CodeQualityProviderRequest request,
        CancellationToken cancellationToken);
}
