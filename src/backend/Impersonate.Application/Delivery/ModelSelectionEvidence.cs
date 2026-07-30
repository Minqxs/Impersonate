namespace Impersonate.Application.Delivery;

public sealed record ModelSelectionEvidence(Guid DecisionId, string SelectionSource, int Score, string Explanation, string MetadataVersion, string ScoreBreakdownJson);
