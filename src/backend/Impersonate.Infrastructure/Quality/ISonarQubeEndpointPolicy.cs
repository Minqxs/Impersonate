namespace Impersonate.Infrastructure.Quality;

internal interface ISonarQubeEndpointPolicy
{
    Task<(bool Allowed, string? Code, string? Message)> ValidateAsync(
        Uri uri,
        CancellationToken cancellationToken);
}
