using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlannerOptions
{
    public string Provider { get; init; } = "Anthropic";
    public string Model { get; init; } = "";
    public string PromptVersion { get; init; } = "planner-v2";
    public int MaximumTasks { get; init; } = 12;
    public int MaximumPlanningAttempts { get; init; } = 2;
    public int MaximumOutputTokens { get; init; } = 4000;
    public int TimeoutSeconds { get; init; } = 45;
    public int PollIntervalSeconds { get; init; } = 5;
}
