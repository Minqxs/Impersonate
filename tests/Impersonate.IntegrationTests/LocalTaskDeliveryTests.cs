using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Impersonate.Application.Delivery;
using Impersonate.Application.Execution;
using Impersonate.Application.Projects;
using Impersonate.Domain.Delivery;
using Impersonate.Domain.Projects;
using Impersonate.Infrastructure.Delivery;
using Impersonate.Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class LocalTaskDeliveryTests
{
    [Fact]
    public async Task Creates_one_local_commit_without_updating_target_remote()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-delivery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source"); Directory.CreateDirectory(source);
            Git(source, "init", "-b", "main"); Git(source, "config", "user.name", "Test"); Git(source, "config", "user.email", "test@example.invalid");
            await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "before\n"); Git(source, "add", "README.md"); Git(source, "commit", "-m", "initial");
            var baseSha = Git(source, "rev-parse", "HEAD").Trim();
            await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "after\n"); var patch = Git(source, "diff", "--", "README.md"); Git(source, "restore", "README.md");
            var patchSha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(patch))).ToLowerInvariant();
            var project = Project.Create("Local", null, "https://github.com/owner/repository", "main");
            typeof(Project).GetProperty(nameof(Project.RepositoryUrl), BindingFlags.Instance | BindingFlags.Public)!.SetValue(project, source);
            var delivery = TaskDelivery.Create(project.Id, Guid.NewGuid(), Guid.NewGuid(), 1, baseSha, "artifact:patch", patchSha, Guid.NewGuid());
            var handoff = new ApprovedTaskHandoff(project.Id, delivery.PipelineRunId, delivery.PlannedTaskId, 1, "Update readme", "description", [], [], baseSha, "artifact:patch", patchSha, ["README.md"], [], delivery.ApprovedReviewDecisionId, "reviewer", "model", "approved", "coder", "model", Evidence(), Evidence(), Guid.NewGuid(), 1, 0);
            var repository = new DeliveryRepository(delivery);
            var options = Options.Create(new ExecutionOptions { DeliveryRoot = Path.Combine(root, "delivery"), CommandTimeoutSeconds = 30 });
            var registry = new DeliveryWorkspaceRegistry();
            var process = new SafeProcess(new ProcessEnvironment(), NullLogger<SafeProcess>.Instance);
            var service = new LocalTargetRepositoryDeliveryService(new ProjectRepository(project), repository, new ArtifactStore(patch), new Validation(), registry, process, options);

            var result = await service.DeliverApprovedPatchAsync(delivery, handoff, default);

            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(TaskDeliveryStatus.Committed, delivery.Status);
            Assert.Equal(baseSha, delivery.DeliveryBaseCommitSha);
            Assert.Equal(baseSha, Git(source, "rev-parse", "main").Trim());
            Assert.False(Git(source, "branch", "--list", result.Value!.BranchName).Contains(result.Value.BranchName, StringComparison.Ordinal));
            Assert.True(repository.SaveCount >= 4);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal);
                Directory.Delete(root, true);
            }
        }
    }

    private static ModelSelectionEvidence Evidence() => new(Guid.NewGuid(), "AutomaticRouting", 1, "test", "v1", "[]");
    private static string Git(string cwd, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(error); return output;
    }
    private sealed class ProcessEnvironment : IChildProcessEnvironmentBuilder
    {
        public IReadOnlyDictionary<string, string> Build() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = Environment.GetEnvironmentVariable("PATH")!, ["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot")! };
    }
    private sealed class ProjectRepository(Project project) : IProjectRepository
    {
        public Task<Project?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<Project?>(project); public Task AddAsync(Project p, CancellationToken ct) => Task.CompletedTask; public Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, string? search, CancellationToken ct) => Task.FromResult<IReadOnlyList<Project>>([project]); public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class DeliveryRepository(TaskDelivery delivery) : ITaskDeliveryRepository
    {
        public int SaveCount { get; private set; }
        public Task<TaskDelivery?> GetByTaskAsync(Guid p, Guid r, Guid t, CancellationToken ct) => Task.FromResult<TaskDelivery?>(delivery); public Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<IReadOnlyList<TaskDelivery>>([delivery]); public Task AddAsync(TaskDelivery d, CancellationToken ct) => Task.CompletedTask; public Task<TaskDelivery?> ClaimNextPendingAsync(Guid id, string owner, DateTimeOffset at, DateTimeOffset expires, CancellationToken ct) => Task.FromResult<TaskDelivery?>(delivery); public Task SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.CompletedTask; }
    }
    private sealed class ArtifactStore(string patch) : IExecutionArtifactStore
    {
        public Task<string> ReadTextAsync(string reference, int maximumCharacters, CancellationToken ct) => Task.FromResult(patch); public Task<StoredArtifact> WriteTextAsync(ArtifactScope scope, string name, string content, string mediaType, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Validation : IDeliveryValidationService
    {
        public Task<DeliveryOperationResult<IReadOnlyList<DeliveryValidationStep>>> ValidateAsync(DeliveryWorkspaceReference workspace, CancellationToken ct) => Task.FromResult(DeliveryOperationResult<IReadOnlyList<DeliveryValidationStep>>.Ok([new("test", true, "passed")]));
    }
}
