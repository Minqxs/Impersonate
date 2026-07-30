using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlannerValidationError(string Code, string Message, int? TaskSequence = null, string? OffendingPath = null);
