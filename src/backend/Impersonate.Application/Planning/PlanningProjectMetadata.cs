using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlanningProjectMetadata(string Path, IReadOnlyList<string> ProjectReferences, IReadOnlyList<string> RecognisedTestPackages, bool IsTestProject, bool ManifestAccessible, bool IncludedInRelevantExcerpts);
