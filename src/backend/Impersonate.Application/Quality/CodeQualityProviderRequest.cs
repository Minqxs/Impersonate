namespace Impersonate.Application.Quality;

public sealed record CodeQualityProviderRequest(Uri BaseUri, string ProjectKey, string Token);
