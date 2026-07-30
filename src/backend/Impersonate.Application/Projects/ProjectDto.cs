using Impersonate.Domain.Projects;

namespace Impersonate.Application.Projects;

public sealed record ProjectDto(Guid Id, string Name, string? Description, string RepositoryUrl, string DefaultBranch, ProjectStatus Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
