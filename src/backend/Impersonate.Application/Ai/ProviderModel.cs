using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ProviderModel(string Id, string Name, string? Description, ModelLifecycleStatus Lifecycle, ModelCapability Capabilities, CapabilityMetadataSource CapabilitySource, int? ContextWindow, int? MaximumOutput);
