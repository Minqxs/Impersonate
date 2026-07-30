using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlanningRepositoryContextResult(bool Succeeded, PlanningRepositoryContext? Context, string? FailureCode, string? FailureMessage);
