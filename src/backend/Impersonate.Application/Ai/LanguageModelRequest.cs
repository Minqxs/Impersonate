using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record LanguageModelRequest(string Model, string SystemInstructions, string UserContent, string JsonSchema, int MaximumOutputTokens, string? ReasoningEffort = null, string? TextVerbosity = null);
