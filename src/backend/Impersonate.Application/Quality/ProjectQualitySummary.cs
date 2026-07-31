namespace Impersonate.Application.Quality;

public sealed record ProjectQualitySummary(
    ProjectQualityState State, string? QualityGate, QualityMetric Coverage,
    QualityMetric NewCoverage, QualityMetric Bugs, QualityMetric Vulnerabilities,
    QualityMetric CodeSmells, QualityMetric Reliability, QualityMetric Security,
    QualityMetric Maintainability, QualityMetric DuplicatedLines, QualityMetric LinesOfCode,
    QualityMetric CognitiveComplexity, DateTimeOffset? LastSuccessfulRefreshAtUtc,
    string? FailureCode, string? SafeMessage, string? ProjectUrl);
