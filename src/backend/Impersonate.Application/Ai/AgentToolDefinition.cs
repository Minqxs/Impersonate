using System.Text.Json;

namespace Impersonate.Application.Ai;

public sealed record AgentToolDefinition(string Name, string Description, JsonElement Parameters, bool Strict = true);
