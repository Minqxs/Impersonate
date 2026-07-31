namespace Impersonate.Application.Quality;

public enum ProjectQualityState
{
    NotConfigured,
    Loading,
    Available,
    Passed,
    Failed,
    TemporarilyUnavailable,
    AuthenticationRequired,
    ProjectNotFound
}
