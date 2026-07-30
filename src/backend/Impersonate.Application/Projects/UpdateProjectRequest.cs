using Impersonate.Domain.Projects;

namespace Impersonate.Application.Projects;

public sealed record UpdateProjectRequest(string Name, string? Description, string RepositoryUrl, string DefaultBranch);
