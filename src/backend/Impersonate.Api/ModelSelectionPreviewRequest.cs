using Impersonate.Domain.Ai;

namespace Impersonate.Api;

public sealed record ModelSelectionPreviewRequest(AgentRole Role, string Description, Guid? ManualModelOverrideId);
