namespace Impersonate.Domain.Ai;

public enum ProviderType
{
    Anthropic,
    OpenAI,
    GoogleGemini,
    OpenRouter,
    AzureOpenAI,
    AmazonBedrock,
    Ollama
}

public enum ProviderConnectionStatus { PendingValidation, Connected, InvalidCredentials, Unavailable, Disabled }
public enum AgentRole { Planner, Coder, Reviewer }
public enum ModelLifecycleStatus { Unknown, Stable, Preview, Deprecated }
public enum CapabilityMetadataSource { LiveProviderMetadata, VersionedProviderMapping, ConservativeDefault }
public enum RoutingPreference { Economy, Balanced, Quality }
public enum ModelSelectionSource { AutomaticRouting, ManualOverride, Escalation, EnvironmentFallback }
