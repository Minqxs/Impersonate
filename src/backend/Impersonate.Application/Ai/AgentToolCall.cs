namespace Impersonate.Application.Ai;

public sealed record AgentToolCall(string CallId, string Name, string ArgumentsJson);
