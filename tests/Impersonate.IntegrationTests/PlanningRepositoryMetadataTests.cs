using Impersonate.Application.Execution;
using Impersonate.Infrastructure.Agents.Planner;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class PlanningRepositoryMetadataTests
{
    [Fact]
    public async Task TaskIt_like_test_project_is_discovered_outside_bounded_excerpts()
    {
        var paths = Enumerable.Range(1, 35).Select(x => $"backend/src/Domain/EmailDomain{x}.cs").Concat([
            "backend/TaskIt.sln",
            "backend/src/Domain/Domain.csproj",
            "backend/tests/Domain.Tests/Domain.Tests.csproj",
            "backend/tests/Domain.Tests/UserTests.cs"
        ]).ToArray();
        var tools = new SnapshotTools(paths, new Dictionary<string, string>
        {
            ["backend/src/Domain/Domain.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\" />",
            ["backend/tests/Domain.Tests/Domain.Tests.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"../../src/Domain/Domain.csproj\" /><PackageReference Include=\"Microsoft.NET.Test.Sdk\" /></ItemGroup></Project>"
        });
        var result = await new PlanningRepositoryContextService(new Workspace(), tools, new Artifacts()).BuildAsync(Guid.NewGuid(), Guid.NewGuid(), "repo", "main", "EmailDomain", default);

        Assert.True(result.Succeeded, result.FailureMessage);
        Assert.Contains("backend/TaskIt.sln", result.Context!.SolutionPaths!);
        var testProject = Assert.Single(result.Context.Projects!, x => x.IsTestProject);
        Assert.True(testProject.ManifestAccessible);
        Assert.False(testProject.IncludedInRelevantExcerpts);
        Assert.Contains("Microsoft.NET.Test.Sdk", testProject.RecognisedTestPackages);
        Assert.Contains("../../src/Domain/Domain.csproj", testProject.ProjectReferences);
        Assert.Equal("TestProjectOutsideRelevantExcerpts", result.Context.TestProjectEvidence);
    }

    [Fact]
    public async Task Inaccessible_test_manifest_is_distinct_from_no_test_project()
    {
        var inaccessible = await Build(["backend/src/Domain/Domain.csproj", "backend/tests/Domain.Tests/Domain.Tests.csproj"], new Dictionary<string, string> { ["backend/src/Domain/Domain.csproj"] = "<Project />" });
        var absent = await Build(["backend/src/Domain/Domain.csproj"], new Dictionary<string, string> { ["backend/src/Domain/Domain.csproj"] = "<Project />" });

        Assert.Equal("TestProjectManifestInaccessible", inaccessible.TestProjectEvidence);
        Assert.Equal("NoTestProjectFound", absent.TestProjectEvidence);
    }

    [Fact]
    public async Task Capped_manifest_scan_never_claims_that_no_test_project_exists()
    {
        var paths = Enumerable.Range(1, 101).Select(x => $"src/Project{x:D3}/Project{x:D3}.csproj").ToArray();
        var files = paths.ToDictionary(x => x, _ => "<Project />");
        var context = await Build(paths, files);

        Assert.Equal(100, context.Projects!.Count);
        Assert.Equal("TestProjectScanTruncated", context.TestProjectEvidence);
    }

    private static async Task<Impersonate.Application.Planning.PlanningRepositoryContext> Build(string[] paths, IReadOnlyDictionary<string, string> files)
    {
        var result = await new PlanningRepositoryContextService(new Workspace(), new SnapshotTools(paths, files), new Artifacts()).BuildAsync(Guid.NewGuid(), Guid.NewGuid(), "repo", "main", "feature", default);
        Assert.True(result.Succeeded, result.FailureMessage);
        return result.Context!;
    }

    private sealed class Workspace : IRepositoryWorkspaceService
    {
        public Task<WorkspacePreparationResult> PrepareAsync(WorkspaceRequest request, CancellationToken ct) => Task.FromResult(new WorkspacePreparationResult(true, new("workspace"), null, null));
        public Task CleanupAsync(WorkspaceReference workspace, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class SnapshotTools(IReadOnlyList<string> paths, IReadOnlyDictionary<string, string> files) : IRepositoryTools
    {
        public Task<RepositoryToolResult> ListFilesAsync(WorkspaceReference workspace, string relativePath, CancellationToken ct) => Task.FromResult(new RepositoryToolResult(true, string.Join('\n', paths)));
        public Task<RepositoryToolResult> ReadFileAsync(WorkspaceReference workspace, string relativePath, CancellationToken ct) => Task.FromResult(files.TryGetValue(relativePath, out var value) ? new RepositoryToolResult(true, value) : relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? new RepositoryToolResult(true, "public sealed class User {}") : new RepositoryToolResult(false, "", "read_failed", "Manifest inaccessible."));
        public Task<RepositoryToolResult> SearchTextAsync(WorkspaceReference workspace, string query, string relativePath, CancellationToken ct) => throw new NotSupportedException();
        public Task<RepositoryToolResult> ApplyPatchAsync(WorkspaceReference workspace, string patch, CancellationToken ct) => throw new NotSupportedException();
        public Task<RepositoryToolResult> GetDiffAsync(WorkspaceReference workspace, CancellationToken ct) => throw new NotSupportedException();
        public Task<RepositoryToolResult> RunCommandAsync(WorkspaceReference workspace, RepositoryCommand command, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Artifacts : IExecutionArtifactStore
    {
        public Task<StoredArtifact> WriteTextAsync(ArtifactScope scope, string name, string content, string mediaType, CancellationToken ct) => Task.FromResult(new StoredArtifact("artifact", "sha", content.Length, mediaType, DateTimeOffset.UtcNow));
        public Task<string> ReadTextAsync(string reference, int maximumCharacters, CancellationToken ct) => throw new NotSupportedException();
    }
}
