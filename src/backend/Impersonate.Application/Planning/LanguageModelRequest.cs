using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record LanguageModelRequest(string Model, string SystemInstructions, string UserContent, string JsonSchema, int MaximumOutputTokens);
