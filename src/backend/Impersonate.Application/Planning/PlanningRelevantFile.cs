using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlanningRelevantFile(string Path, string Content, bool Truncated);
