using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

[Flags]
public enum ModelCapability
{
    None = 0,
    TextGeneration = 1,
    StructuredOutput = 2,
    Reasoning = 4,
    Coding = 8,
    ToolUse = 16,
    LargeContext = 32,
    FastResponse = 64,
    LowCost = 128,
    Vision = 256
}
