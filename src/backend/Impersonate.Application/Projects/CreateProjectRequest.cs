using Impersonate.Domain.Projects;

namespace Impersonate.Application.Projects;

public sealed record CreateProjectRequest(string Name, string? Description, string RepositoryUrl, string DefaultBranch, ProjectStatus Status = ProjectStatus.Idle);
