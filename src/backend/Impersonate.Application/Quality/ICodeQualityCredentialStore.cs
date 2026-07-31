namespace Impersonate.Application.Quality;

public interface ICodeQualityCredentialStore
{
    Task StoreAsync(Guid configurationId, string token, CancellationToken cancellationToken);
    Task<string?> RetrieveAsync(Guid configurationId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid configurationId, CancellationToken cancellationToken);
}
