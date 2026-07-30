using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record TaskModelOverridesRequest(Guid? CoderModelId, Guid? ReviewerModelId);
