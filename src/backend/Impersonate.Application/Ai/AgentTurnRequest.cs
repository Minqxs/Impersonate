namespace Impersonate.Application.Ai;

public sealed record AgentTurnRequest(
    string Model,
    string SystemInstructions,
    string? InitialInput,
    IReadOnlyList<AgentToolDefinition> Tools,
    IReadOnlyList<AgentToolResult> ToolResults,
    AgentConversationReference? Conversation,
    int MaximumOutputTokens,
    string? ReasoningEffort = null,
    string? TextVerbosity = null);
