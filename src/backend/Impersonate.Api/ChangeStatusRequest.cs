using Impersonate.Domain.Projects;

namespace Impersonate.Api;

public sealed record ChangeStatusRequest(ProjectStatus Status);
