using System.Diagnostics;
using Impersonate.Application.Execution;
using Impersonate.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class ExecutionWorkspaceTests
{
    [Fact]
    public async Task Generated_patch_ignores_git_presentation_configuration()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-diff-config-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        try
        {
            Run("git", ["init", "-b", "main"], source);
            Run("git", ["config", "user.email", "fixture@example.test"], source);
            Run("git", ["config", "user.name", "Fixture"], source);
            var relative = "nested/file with space.txt";
            var file = Path.Combine(source, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, "before\n");
            Run("git", ["add", "."], source);
            Run("git", ["commit", "-m", "baseline"], source);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Execution:WorkspaceRoot"] = Path.Combine(root, "workspaces"), ["Execution:ArtifactRoot"] = Path.Combine(root, "artifacts"), ["Ai:DataProtectionKeyPath"] = Path.Combine(root, "keys") }).Build();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration, new TestEnvironment());
            await using var provider = services.BuildServiceProvider();
            var prepared = await provider.GetRequiredService<IRepositoryWorkspaceService>().PrepareAsync(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, source, "main", [], null), default);
            Assert.True(prepared.Succeeded, prepared.FailureMessage);
            var workspace = workspacesPath(prepared.Workspace!, root);
            Run("git", ["config", "diff.noprefix", "true"], workspace);
            Run("git", ["config", "color.ui", "always"], workspace);
            Run("git", ["config", "core.quotePath", "true"], workspace);
            await File.WriteAllTextAsync(Path.Combine(workspace, relative.Replace('/', Path.DirectorySeparatorChar)), "after\n");
            var diff = await provider.GetRequiredService<IRepositoryTools>().GetDiffAsync(prepared.Workspace!, default);
            Assert.True(diff.Succeeded, diff.FailureMessage);
            Assert.Contains($"diff --git a/{relative} b/{relative}", diff.Output);
            Assert.DoesNotContain("\u001b[", diff.Output);
        }
        finally { if (Directory.Exists(root)) { foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal); Directory.Delete(root, true); } }
    }

    [Fact]
    public void Sanitized_environment_uses_explicit_allowlist_and_excludes_secrets()
    {
        var proxyName = "HTTPS_PROXY";
        var secretName = "IMPERSONATE_TEST_API_KEY";
        var oldProxy = Environment.GetEnvironmentVariable(proxyName);
        var oldSecret = Environment.GetEnvironmentVariable(secretName);
        try
        {
            Environment.SetEnvironmentVariable(proxyName, "http://proxy.example.test:8080");
            Environment.SetEnvironmentVariable(secretName, "never-copy-this");
            var environment = new Impersonate.Infrastructure.Execution.AllowlistedChildProcessEnvironmentBuilder().Build();
            Assert.Equal("http://proxy.example.test:8080", environment[proxyName]);
            Assert.DoesNotContain(secretName, environment.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(environment.Keys, x => x.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) || x.Contains("API_KEY", StringComparison.OrdinalIgnoreCase));
            if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("SystemRoot") is { } systemRoot)
            {
                Assert.Equal(systemRoot, environment["systemroot"]);
                Assert.Contains("SystemRoot", environment.Keys, StringComparer.OrdinalIgnoreCase);
            }
        }
        finally { Environment.SetEnvironmentVariable(proxyName, oldProxy); Environment.SetEnvironmentVariable(secretName, oldSecret); }
    }

    [Fact]
    public async Task Execution_readiness_starts_sanitized_git_and_validates_workspace_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-readiness-" + Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Execution:WorkspaceRoot", root }, { "Execution:ArtifactRoot", Path.Combine(root, "artifacts") }, { "Ai:DataProtectionKeyPath", Path.Combine(root, "keys") } }).Build();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration, new TestEnvironment());
            await using var provider = services.BuildServiceProvider();
            var result = await provider.GetRequiredService<IExecutionEnvironmentReadinessService>().CheckAsync(default);
            Assert.True(result.Ready, string.Join(" ", result.Blockers));
            Assert.True(result.GitAvailable);
            Assert.True(result.GitVersionSucceeded);
            Assert.True(result.SanitizedProcessSucceeded);
            Assert.True(result.WorkspaceRootWritable);
            if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("SystemRoot") is not null)
            {
                Assert.True(result.CoreEnvironmentValid);
                Assert.Contains("SystemRoot", result.SuppliedVariableNames, StringComparer.OrdinalIgnoreCase);
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Dependent_task_patch_is_incremental_to_composed_dependency_baseline()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-incremental-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        try
        {
            Run("git", ["init", "-b", "main"], source);
            Run("git", ["config", "user.email", "fixture@example.test"], source);
            Run("git", ["config", "user.name", "Fixture"], source);
            await File.WriteAllTextAsync(Path.Combine(source, "User.cs"), "public sealed class User\n{\n}\n");
            Run("git", ["add", "User.cs"], source);
            Run("git", ["commit", "-m", "fixture baseline"], source);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Execution:WorkspaceRoot", Path.Combine(root, "workspaces") }, { "Execution:ArtifactRoot", Path.Combine(root, "artifacts") }, { "Ai:DataProtectionKeyPath", Path.Combine(root, "keys") } }).Build();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration, new TestEnvironment());
            await using var provider = services.BuildServiceProvider();
            var workspaceService = provider.GetRequiredService<IRepositoryWorkspaceService>();
            var tools = provider.GetRequiredService<IRepositoryTools>();
            var artifacts = provider.GetRequiredService<IExecutionArtifactStore>();
            var scope = new ArtifactScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
            var task1 = "diff --git a/User.cs b/User.cs\nindex 3bd1f0e..28d9240 100644\n--- a/User.cs\n+++ b/User.cs\n@@ -1,3 +1,4 @@\n public sealed class User\n {\n+    public bool IsActive { get; set; } = true;\n }\n";
            var stored = await artifacts.WriteTextAsync(scope, "task-1.patch", task1, "text/x-diff", default);
            var dependencyId = Guid.NewGuid();
            var prepared = await workspaceService.PrepareAsync(new(scope.ProjectId, scope.PipelineRunId, Guid.NewGuid(), 1, source, "main", [new(dependencyId, 1, stored.Reference)], null), default);
            Assert.True(prepared.Succeeded, prepared.FailureMessage);
            Assert.Contains("IsActive", (await tools.ReadFileAsync(prepared.Workspace!, "User.cs", default)).Output);
            Assert.Equal([dependencyId], prepared.DependencyTaskIds);
            Assert.False(string.IsNullOrWhiteSpace(prepared.SourceBaseCommitSha));
            Assert.False(string.IsNullOrWhiteSpace(prepared.ComposedTreeFingerprint));
            var task2 = "diff --git a/UserProfileDto.cs b/UserProfileDto.cs\nnew file mode 100644\nindex 0000000..e20cb0c\n--- /dev/null\n+++ b/UserProfileDto.cs\n@@ -0,0 +1 @@\n+public sealed record UserProfileDto(string Name);\n";
            Assert.True((await tools.ApplyPatchAsync(prepared.Workspace!, task2, default)).Succeeded);
            var diff = await tools.GetDiffAsync(prepared.Workspace!, default);
            Assert.True(diff.Succeeded, diff.FailureMessage);
            Assert.Contains("UserProfileDto.cs", diff.Output);
            Assert.DoesNotContain("IsActive", diff.Output);
            var composed = Path.Combine(root, "composed");
            Run("git", ["clone", "--branch", "main", "--", source, composed], root);
            RunWithInput(["apply", "--whitespace=nowarn", "-"], composed, task1);
            RunWithInput(["apply", "--whitespace=nowarn", "-"], composed, diff.Output);
            Assert.Contains("IsActive", await File.ReadAllTextAsync(Path.Combine(composed, "User.cs")));
            Assert.True(File.Exists(Path.Combine(composed, "UserProfileDto.cs")));
            Assert.Equal(1, Directory.EnumerateDirectories(Path.Combine(composed, ".git", "refs", "heads")).Count() + Directory.EnumerateFiles(Path.Combine(composed, ".git", "refs", "heads")).Count());
        }
        finally { if (Directory.Exists(root)) { foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal); Directory.Delete(root, true); } }
    }
    [Fact]
    public async Task Revision_patch_is_a_full_replacement_relative_to_dependencies()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-revision-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        try
        {
            Run("git", ["init", "-b", "main"], source);
            Run("git", ["config", "user.email", "fixture@example.test"], source);
            Run("git", ["config", "user.name", "Fixture"], source);
            await File.WriteAllTextAsync(Path.Combine(source, "User.cs"), "public sealed class User\n{\n}\n");
            Run("git", ["add", "User.cs"], source);
            Run("git", ["commit", "-m", "baseline"], source);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Execution:WorkspaceRoot", Path.Combine(root, "workspaces") }, { "Execution:ArtifactRoot", Path.Combine(root, "artifacts") } }).Build();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration, new TestEnvironment());
            await using var provider = services.BuildServiceProvider();
            var artifacts = provider.GetRequiredService<IExecutionArtifactStore>();
            var workspaces = provider.GetRequiredService<IRepositoryWorkspaceService>();
            var tools = provider.GetRequiredService<IRepositoryTools>();
            var scope = new ArtifactScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
            var dependencyPatch = "diff --git a/User.cs b/User.cs\n--- a/User.cs\n+++ b/User.cs\n@@ -1,3 +1,4 @@\n public sealed class User\n {\n+    public bool IsActive { get; set; } = true;\n }\n";
            var revisionPatch = "diff --git a/UserProfileDto.cs b/UserProfileDto.cs\nnew file mode 100644\n--- /dev/null\n+++ b/UserProfileDto.cs\n@@ -0,0 +1 @@\n+public sealed record UserProfileDto(string Name);\n";
            var dependency = await artifacts.WriteTextAsync(scope, "dependency.patch", dependencyPatch, "text/x-diff", default);
            var revision = await artifacts.WriteTextAsync(scope, "revision.patch", revisionPatch, "text/x-diff", default);
            var prepared = await workspaces.PrepareAsync(new(scope.ProjectId, scope.PipelineRunId, scope.PlannedTaskId, 2, source, "main", [new(Guid.NewGuid(), 1, dependency.Reference)], revision.Reference), default);
            Assert.True(prepared.Succeeded, prepared.FailureMessage);
            Assert.True(prepared.CurrentRevisionPatchApplied);
            await File.WriteAllTextAsync(Path.Combine(workspacesPath(prepared.Workspace!, root), "UserProfileDto.cs"), "public sealed record UserProfileDto(string Name, bool IsActive);\n");
            var replacement = await tools.GetDiffAsync(prepared.Workspace!, default);
            Assert.Contains("UserProfileDto", replacement.Output);
            Assert.Contains("bool IsActive", replacement.Output);
            Assert.DoesNotContain("public bool IsActive", replacement.Output);
        }
        finally { if (Directory.Exists(root)) { foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal); Directory.Delete(root, true); } }
    }
    [Fact]
    public async Task Three_task_dependency_chain_produces_apply_once_incremental_patches()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-three-task-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        try
        {
            Run("git", ["init", "-b", "main"], source);
            Run("git", ["config", "user.email", "fixture@example.test"], source);
            Run("git", ["config", "user.name", "Fixture"], source);
            await File.WriteAllTextAsync(Path.Combine(source, "User.cs"), "public sealed class User\n{\n}\n");
            Run("git", ["add", "User.cs"], source);
            Run("git", ["commit", "-m", "baseline"], source);
            var sourceHead = Run("git", ["rev-parse", "HEAD"], source).Trim();
            var sourceBranches = Run("git", ["branch", "--format=%(refname:short)"], source).Trim();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Execution:WorkspaceRoot", Path.Combine(root, "workspaces") }, { "Execution:ArtifactRoot", Path.Combine(root, "artifacts") }, { "Ai:DataProtectionKeyPath", Path.Combine(root, "keys") } }).Build();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration, new TestEnvironment());
            await using var provider = services.BuildServiceProvider();
            var workspaces = provider.GetRequiredService<IRepositoryWorkspaceService>();
            var tools = provider.GetRequiredService<IRepositoryTools>();
            var artifacts = provider.GetRequiredService<IExecutionArtifactStore>();
            var projectId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            var task1Id = Guid.NewGuid();
            var task2Id = Guid.NewGuid();
            var task3Id = Guid.NewGuid();

            var task1 = await workspaces.PrepareAsync(new(projectId, runId, task1Id, 1, source, "main", [], null), default);
            Assert.True(task1.Succeeded, task1.FailureMessage);
            await File.WriteAllTextAsync(Path.Combine(workspacesPath(task1.Workspace!, root), "User.cs"), "public sealed class User\n{\n    public bool IsActive { get; set; } = true;\n}\n");
            var p1 = (await tools.GetDiffAsync(task1.Workspace!, default)).Output;
            Assert.Contains("IsActive", p1);
            var p1Artifact = await artifacts.WriteTextAsync(new(projectId, runId, task1Id, 1), "task.patch", p1, "text/x-diff", default);

            var task2 = await workspaces.PrepareAsync(new(projectId, runId, task2Id, 1, source, "main", [new(task1Id, 1, p1Artifact.Reference)], null), default);
            Assert.True(task2.Succeeded, task2.FailureMessage);
            Assert.Contains("IsActive", (await tools.ReadFileAsync(task2.Workspace!, "User.cs", default)).Output);
            Assert.Equal([task1Id], task2.DependencyTaskIds);
            await File.WriteAllTextAsync(Path.Combine(workspacesPath(task2.Workspace!, root), "UserProfileDto.cs"), "public sealed record UserProfileDto(string Name);\n");
            var initialP2 = (await tools.GetDiffAsync(task2.Workspace!, default)).Output;
            Assert.Contains("UserProfileDto", initialP2);
            Assert.DoesNotContain("IsActive", initialP2);
            var initialP2Artifact = await artifacts.WriteTextAsync(new(projectId, runId, task2Id, 1), "task.patch", initialP2, "text/x-diff", default);

            var task2Revision = await workspaces.PrepareAsync(new(projectId, runId, task2Id, 2, source, "main", [new(task1Id, 1, p1Artifact.Reference)], initialP2Artifact.Reference), default);
            Assert.True(task2Revision.Succeeded, task2Revision.FailureMessage);
            Assert.True(task2Revision.CurrentRevisionPatchApplied);
            Assert.Contains("IsActive", (await tools.ReadFileAsync(task2Revision.Workspace!, "User.cs", default)).Output);
            await File.WriteAllTextAsync(Path.Combine(workspacesPath(task2Revision.Workspace!, root), "UserProfileDto.cs"), "public sealed record UserProfileDto(string Name, bool IsActive);\n");
            var revisedP2 = (await tools.GetDiffAsync(task2Revision.Workspace!, default)).Output;
            Assert.Contains("bool IsActive", revisedP2);
            Assert.DoesNotContain("public bool IsActive", revisedP2);
            var revisedP2Artifact = await artifacts.WriteTextAsync(new(projectId, runId, task2Id, 2), "task.patch", revisedP2, "text/x-diff", default);

            var task3 = await workspaces.PrepareAsync(new(projectId, runId, task3Id, 1, source, "main", [new(task1Id, 1, p1Artifact.Reference), new(task2Id, 2, revisedP2Artifact.Reference)], null), default);
            Assert.True(task3.Succeeded, task3.FailureMessage);
            Assert.Equal([task1Id, task2Id], task3.DependencyTaskIds);
            Assert.Equal(2, task3.DependencyTaskIds!.Count);
            Assert.False(string.IsNullOrWhiteSpace(task3.SourceBaseCommitSha));
            Assert.False(string.IsNullOrWhiteSpace(task3.ComposedTreeFingerprint));
            Assert.Contains("IsActive", (await tools.ReadFileAsync(task3.Workspace!, "User.cs", default)).Output);
            Assert.Contains("bool IsActive", (await tools.ReadFileAsync(task3.Workspace!, "UserProfileDto.cs", default)).Output);
            await File.WriteAllTextAsync(Path.Combine(workspacesPath(task3.Workspace!, root), "ProfileService.cs"), "public sealed class ProfileService {}\n");
            var p3 = (await tools.GetDiffAsync(task3.Workspace!, default)).Output;
            Assert.Contains("ProfileService", p3);
            Assert.DoesNotContain("UserProfileDto", p3);
            Assert.DoesNotContain("public bool IsActive", p3);

            var composed = Path.Combine(root, "composed");
            Run("git", ["clone", "--branch", "main", "--", source, composed], root);
            RunWithInput(["apply", "--whitespace=nowarn", "-"], composed, p1);
            RunWithInput(["apply", "--whitespace=nowarn", "-"], composed, revisedP2);
            RunWithInput(["apply", "--whitespace=nowarn", "-"], composed, p3);
            Assert.False(TryRunWithInput(["apply", "--whitespace=nowarn", "-"], composed, p1));
            Assert.False(TryRunWithInput(["apply", "--whitespace=nowarn", "-"], composed, revisedP2));
            Assert.False(TryRunWithInput(["apply", "--whitespace=nowarn", "-"], composed, p3));
            Assert.Contains("IsActive", await File.ReadAllTextAsync(Path.Combine(composed, "User.cs")));
            Assert.Contains("bool IsActive", await File.ReadAllTextAsync(Path.Combine(composed, "UserProfileDto.cs")));
            Assert.True(File.Exists(Path.Combine(composed, "ProfileService.cs")));
            Assert.Equal(sourceHead, Run("git", ["rev-parse", "HEAD"], source).Trim());
            Assert.Equal(sourceBranches, Run("git", ["branch", "--format=%(refname:short)"], source).Trim());
            Assert.Empty(Run("git", ["status", "--porcelain"], source).Trim());
            Assert.Equal(sourceHead, Run("git", ["rev-parse", "HEAD"], composed).Trim());
        }
        finally { if (Directory.Exists(root)) { foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal); Directory.Delete(root, true); } }
    }
    [Fact]
    public async Task Missing_dependency_patch_blocks_with_safe_task_specific_failure()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-missing-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        try
        {
            Run("git", ["init", "-b", "main"], source);
            Run("git", ["config", "user.email", "fixture@example.test"], source);
            Run("git", ["config", "user.name", "Fixture"], source);
            await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "base\n");
            Run("git", ["add", "README.md"], source);
            Run("git", ["commit", "-m", "baseline"], source);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Execution:WorkspaceRoot", Path.Combine(root, "workspaces") }, { "Execution:ArtifactRoot", Path.Combine(root, "artifacts") } }).Build();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration, new TestEnvironment());
            await using var provider = services.BuildServiceProvider();
            var result = await provider.GetRequiredService<IRepositoryWorkspaceService>().PrepareAsync(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, source, "main", [new(Guid.NewGuid(), 7, "artifact:missing")], null), default);
            Assert.False(result.Succeeded);
            Assert.Equal("approved_dependency_patch_missing", result.FailureCode);
            Assert.Equal(7, result.FailingDependencySequence);
            Assert.DoesNotContain(root, result.FailureMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (Directory.Exists(root)) { foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal); Directory.Delete(root, true); } }
    }
    [Fact]
    public async Task Execution_creates_a_real_diff_in_isolation_without_creating_a_commit()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-execution-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        try
        {
            Run("git", ["init", "-b", "main"], source);
            Run("git", ["config", "user.email", "fixture@example.test"], source);
            Run("git", ["config", "user.name", "Fixture"], source);
            await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "baseline\n");
            Run("git", ["add", "README.md"], source);
            Run("git", ["commit", "-m", "fixture baseline"], source);
            var originalHead = Run("git", ["rev-parse", "HEAD"], source).Trim();
            var values = new Dictionary<string, string?> { { "Execution:WorkspaceRoot", Path.Combine(root, "workspaces") }, { "Execution:ArtifactRoot", Path.Combine(root, "artifacts") }, { "Ai:DataProtectionKeyPath", Path.Combine(root, "keys") } };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration, new TestEnvironment());
            await using var provider = services.BuildServiceProvider();
            var workspaceService = provider.GetRequiredService<IRepositoryWorkspaceService>();
            var tools = provider.GetRequiredService<IRepositoryTools>();
            var request = new WorkspaceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, source, "main", [], null);
            var prepared = await workspaceService.PrepareAsync(request, default);
            Assert.True(prepared.Succeeded, prepared.FailureMessage);
            var patch = "diff --git a/feature.txt b/feature.txt\nnew file mode 100644\nindex 0000000..a88e8d8\n--- /dev/null\n+++ b/feature.txt\n@@ -0,0 +1 @@\n+implemented\n";
            var applied = await tools.ApplyPatchAsync(prepared.Workspace!, patch, default);
            Assert.True(applied.Succeeded, applied.FailureMessage);
            var diff = await tools.GetDiffAsync(prepared.Workspace!, default);
            Assert.Contains("feature.txt", diff.Output);
            var workspaceHead = await tools.RunCommandAsync(prepared.Workspace!, new("git", ["rev-parse", "HEAD"]), default);
            Assert.Equal(originalHead, workspaceHead.Output.Trim());
            Assert.Equal(originalHead, Run("git", ["rev-parse", "HEAD"], source).Trim());
            Assert.False(File.Exists(Path.Combine(source, "feature.txt")));
        }
        finally { if (Directory.Exists(root)) { foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal); Directory.Delete(root, true); } }
    }
    [Fact]
    public async Task Public_repository_workspace_smoke_when_explicitly_enabled()
    {
        var repository = Environment.GetEnvironmentVariable("IMPERSONATE_PUBLIC_REPOSITORY_SMOKE_URL");
        if (string.IsNullOrWhiteSpace(repository))
            return;
        var root = Path.Combine(Path.GetTempPath(), "impersonate-public-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Execution:WorkspaceRoot", Path.Combine(root, "workspaces") }, { "Execution:ArtifactRoot", Path.Combine(root, "artifacts") }, { "Ai:DataProtectionKeyPath", Path.Combine(root, "keys") } }).Build();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration, new TestEnvironment());
            await using var provider = services.BuildServiceProvider();
            var prepared = await provider.GetRequiredService<IRepositoryWorkspaceService>().PrepareAsync(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, repository, "main", [], null), default);
            Assert.True(prepared.Succeeded, $"{prepared.FailureCode}: {prepared.FailureMessage}");
            var tools = provider.GetRequiredService<IRepositoryTools>();
            var status = await tools.RunCommandAsync(prepared.Workspace!, new("git", ["status", "--porcelain"]), default);
            Assert.True(status.Succeeded);
            Assert.Empty(status.Output.Trim());
            var head = await tools.RunCommandAsync(prepared.Workspace!, new("git", ["rev-parse", "HEAD"]), default);
            Assert.True(head.Succeeded);
            Assert.NotEmpty(head.Output.Trim());
        }
        finally { if (Directory.Exists(root)) { foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal); Directory.Delete(root, true); } }
    }
    private static string Run(string executable, IReadOnlyList<string> arguments, string cwd)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(error);
        return output;
    }
    private static void RunWithInput(IReadOnlyList<string> arguments, string cwd, string input)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(error);
    }
    private static bool TryRunWithInput(IReadOnlyList<string> arguments, string cwd, string input)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0;
    }
    private static string workspacesPath(WorkspaceReference workspace, string root) => Path.Combine(root, "workspaces", workspace.Value["workspace:".Length..].Replace('/', Path.DirectorySeparatorChar));
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing"; public string ApplicationName { get; set; } = "Impersonate.IntegrationTests"; public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory(); public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
