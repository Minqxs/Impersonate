using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ModelSelectionResult(bool Succeeded, TaskProfile Profile, SelectedModel? Selection, IReadOnlyList<SelectedModel> EligibleAlternatives, string? FailureCode, string? FailureMessage);
