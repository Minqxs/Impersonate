using Impersonate.Domain.Projects;

namespace Impersonate.Application.Projects;

public sealed record ProjectHealthSummaryDto(Guid ProjectId, string OverallStatus, IReadOnlyList<ProjectHealthCheckDto> Checks, DateTimeOffset CheckedAtUtc);
