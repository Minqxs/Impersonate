using Impersonate.Domain.Projects;

namespace Impersonate.Application.Projects;

public sealed record ProjectHealthCheckDto(string Name, string Status, string Message);
