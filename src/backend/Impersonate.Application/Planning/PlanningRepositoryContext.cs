using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlanningRepositoryContext(IReadOnlyList<string> Tree, IReadOnlyList<PlanningRelevantFile> RelevantFiles, IReadOnlyList<string> Languages, IReadOnlyList<string> Frameworks, IReadOnlyList<string> Layers, IReadOnlyList<string> TestLocations, IReadOnlyList<string> MigrationLocations, string Summary, string? ArtifactReference, IReadOnlySet<string> EvidencePaths);
