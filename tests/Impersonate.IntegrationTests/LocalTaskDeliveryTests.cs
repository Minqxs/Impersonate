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
    public async Task Live_crlf_patch_passes_exact_file_set_verification()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-live-patch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            var relative = "backend/src/HomeTaskSA.Domain/Entities/User.cs";
            var file = Path.Combine(source, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            Git(source, "init", "-b", "main");
            Git(source, "config", "user.name", "Test");
            Git(source, "config", "user.email", "test@example.invalid");
            await File.WriteAllTextAsync(file, "before\n");
            Git(source, "add", ".");
            Git(source, "commit", "-m", "initial");
            var baseSha = Git(source, "rev-parse", "HEAD").Trim();
            await File.WriteAllTextAsync(file, "after\n");
            var patch = Git(source, "diff", "--", relative).Replace("\n", "\r\n", StringComparison.Ordinal);
            Git(source, "restore", relative);
            var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(patch))).ToLowerInvariant();
            var project = Project.Create("Live", null, "https://github.com/owner/repository", "main");
            typeof(Project).GetProperty(nameof(Project.RepositoryUrl), BindingFlags.Instance | BindingFlags.Public)!.SetValue(project, source);
            var delivery = TaskDelivery.Create(project.Id, Guid.NewGuid(), Guid.NewGuid(), 1, baseSha, "artifact:live", sha, Guid.NewGuid());
            var handoff = new ApprovedTaskHandoff(project.Id, delivery.PipelineRunId, delivery.PlannedTaskId, 1, "Live path", "description", [], [], baseSha, "artifact:live", sha, [relative], [], delivery.ApprovedReviewDecisionId, "reviewer", "model", "approved", "coder", "model", Evidence(), Evidence(), Guid.NewGuid(), 1, 0);
            var options = Options.Create(new ExecutionOptions { DeliveryRoot = Path.Combine(root, "delivery"), CommandTimeoutSeconds = 30 });
            var result = await new LocalTargetRepositoryDeliveryService(new ProjectRepository(project), new DeliveryRepository(delivery), new RunDeliveryRepository(delivery, baseSha), new ArtifactStore(patch), new Validation(), new DeliveryWorkspaceRegistry(), new SafeProcess(new ProcessEnvironment(), NullLogger<SafeProcess>.Instance), options).DeliverApprovedPatchAsync(delivery, handoff, default);
            Assert.True(result.Succeeded, $"{result.Code}: {result.Error}");
            Assert.Equal(TaskDeliveryStatus.Committed, delivery.Status);
        }
        finally { if (Directory.Exists(root)) { foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal); Directory.Delete(root, true); } }
    }

    [Fact]
    public async Task Creates_one_local_commit_without_updating_target_remote()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-delivery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            Git(source, "init", "-b", "main");
            Git(source, "config", "user.name", "Test");
            Git(source, "config", "user.email", "test@example.invalid");
            await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "before\n");
            Git(source, "add", "README.md");
            Git(source, "commit", "-m", "initial");
            var baseSha = Git(source, "rev-parse", "HEAD").Trim();
            await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "after\n");
            var patch = Git(source, "diff", "--", "README.md");
            Git(source, "restore", "README.md");
            var patchSha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(patch))).ToLowerInvariant();
            var project = Project.Create("Local", null, "https://github.com/owner/repository", "main");
            typeof(Project).GetProperty(nameof(Project.RepositoryUrl), BindingFlags.Instance | BindingFlags.Public)!.SetValue(project, source);
            var delivery = TaskDelivery.Create(project.Id, Guid.NewGuid(), Guid.NewGuid(), 1, baseSha, "artifact:patch", patchSha, Guid.NewGuid());
            var handoff = new ApprovedTaskHandoff(project.Id, delivery.PipelineRunId, delivery.PlannedTaskId, 1, "Update readme", "description", [], [], baseSha, "artifact:patch", patchSha, ["README.md"], [], delivery.ApprovedReviewDecisionId, "reviewer", "model", "approved", "coder", "model", Evidence(), Evidence(), Guid.NewGuid(), 1, 0);
            var repository = new DeliveryRepository(delivery);
            var options = Options.Create(new ExecutionOptions { DeliveryRoot = Path.Combine(root, "delivery"), CommandTimeoutSeconds = 30 });
            var registry = new DeliveryWorkspaceRegistry();
            var process = new SafeProcess(new ProcessEnvironment(), NullLogger<SafeProcess>.Instance);
            var runDeliveries = new RunDeliveryRepository(delivery, baseSha);
            var service = new LocalTargetRepositoryDeliveryService(new ProjectRepository(project), repository, runDeliveries, new ArtifactStore(patch), new Validation(), registry, process, options);

            var result = await service.DeliverApprovedPatchAsync(delivery, handoff, default);
            var replay = await service.DeliverApprovedPatchAsync(delivery, handoff, default);

            var second = TaskDelivery.Create(project.Id, delivery.PipelineRunId, Guid.NewGuid(), 2, baseSha, "artifact:patch", patchSha, Guid.NewGuid());
            var secondHandoff = handoff with
            {
                PlannedTaskId = second.PlannedTaskId,
                TaskSequence = 2,
                Title = "Update readme independently",
                ApprovedReviewDecisionId = second.ApprovedReviewDecisionId
            };
            var secondService = new LocalTargetRepositoryDeliveryService(new ProjectRepository(project), new DeliveryRepository(second), runDeliveries, new ArtifactStore(patch), new Validation(), registry, process, options);
            var secondResult = await secondService.DeliverApprovedPatchAsync(second, secondHandoff, default);

            Assert.True(result.Succeeded, $"{result.Code}: {result.Error}");
            Assert.True(replay.Succeeded, $"{replay.Code}: {replay.Error}");
            Assert.True(secondResult.Succeeded, $"{secondResult.Code}: {secondResult.Error}");
            Assert.Equal(TaskDeliveryStatus.Committed, delivery.Status);
            Assert.Equal(result.Value!.CommitSha, replay.Value!.CommitSha);
            Assert.NotEqual(result.Value.BranchName, secondResult.Value!.BranchName);
            Assert.NotEqual(result.Value.CommitSha, secondResult.Value.CommitSha);
            Assert.Equal(baseSha, delivery.DeliveryBaseCommitSha);
            Assert.Equal(baseSha, Git(source, "rev-parse", "main").Trim());
            Assert.False(Git(source, "branch", "--list", result.Value.BranchName).Contains(result.Value.BranchName, StringComparison.Ordinal));
            var cache = Path.Combine(root, "delivery", "repositories", project.Id.ToString("N"), "repository.git");
            Assert.Equal("2", Git(root, $"--git-dir={cache}", "rev-list", "--count", result.Value.BranchName).Trim());
            Assert.Equal("2", Git(root, $"--git-dir={cache}", "rev-list", "--count", secondResult.Value.BranchName).Trim());
            Assert.True(repository.SaveCount >= 4);

            typeof(Project).GetProperty(nameof(Project.RepositoryUrl), BindingFlags.Instance | BindingFlags.Public)!.SetValue(project, "https://github.com/owner/repository");
            var push = new TaskDeliveryPushService(new ProjectRepository(project), repository, process, options);
            var pushed = await push.PushAsync(delivery, default);
            var pushedAgain = await push.PushAsync(delivery, default);
            var secondPushed = await new TaskDeliveryPushService(new ProjectRepository(project), new DeliveryRepository(second), process, options).PushAsync(second, default);
            Assert.True(pushed.Succeeded, $"{pushed.Code}: {pushed.Error}");
            Assert.True(pushedAgain.Succeeded);
            Assert.True(pushedAgain.Value!.Recovered);
            Assert.True(secondPushed.Succeeded);
            Assert.Equal(result.Value.CommitSha, Git(source, "rev-parse", result.Value.BranchName).Trim());
            Assert.Equal(secondResult.Value.CommitSha, Git(source, "rev-parse", secondResult.Value.BranchName).Trim());
            Assert.Equal(TaskDeliveryStatus.Pushed, delivery.Status);

            typeof(Project).GetProperty(nameof(Project.RepositoryUrl), BindingFlags.Instance | BindingFlags.Public)!.SetValue(project, source);
            var recoveredDelivery = TaskDelivery.Create(project.Id, delivery.PipelineRunId, Guid.NewGuid(), 3, baseSha, "artifact:patch", patchSha, Guid.NewGuid());
            var recoveredHandoff = handoff with
            {
                PlannedTaskId = recoveredDelivery.PlannedTaskId,
                TaskSequence = 3,
                Title = "Recover pushed branch",
                ApprovedReviewDecisionId = recoveredDelivery.ApprovedReviewDecisionId
            };
            var recoveredRepository = new DeliveryRepository(recoveredDelivery);
            var recoveredLocal = await new LocalTargetRepositoryDeliveryService(new ProjectRepository(project), recoveredRepository, runDeliveries, new ArtifactStore(patch), new Validation(), registry, process, options).DeliverApprovedPatchAsync(recoveredDelivery, recoveredHandoff, default);
            Assert.True(recoveredLocal.Succeeded);
            Git(cache, "push", "origin", $"{recoveredLocal.Value!.BranchName}:refs/heads/{recoveredLocal.Value.BranchName}");
            typeof(Project).GetProperty(nameof(Project.RepositoryUrl), BindingFlags.Instance | BindingFlags.Public)!.SetValue(project, "https://github.com/owner/repository");
            var recoveredPush = await new TaskDeliveryPushService(new ProjectRepository(project), recoveredRepository, process, options).PushAsync(recoveredDelivery, default);
            Assert.True(recoveredPush.Succeeded);
            Assert.True(recoveredPush.Value!.Recovered);

            typeof(Project).GetProperty(nameof(Project.RepositoryUrl), BindingFlags.Instance | BindingFlags.Public)!.SetValue(project, source);
            var conflictDelivery = TaskDelivery.Create(project.Id, delivery.PipelineRunId, Guid.NewGuid(), 4, baseSha, "artifact:patch", patchSha, Guid.NewGuid());
            var conflictHandoff = handoff with
            {
                PlannedTaskId = conflictDelivery.PlannedTaskId,
                TaskSequence = 4,
                Title = "Conflict branch",
                ApprovedReviewDecisionId = conflictDelivery.ApprovedReviewDecisionId
            };
            var conflictRepository = new DeliveryRepository(conflictDelivery);
            var conflictLocal = await new LocalTargetRepositoryDeliveryService(new ProjectRepository(project), conflictRepository, runDeliveries, new ArtifactStore(patch), new Validation(), registry, process, options).DeliverApprovedPatchAsync(conflictDelivery, conflictHandoff, default);
            Assert.True(conflictLocal.Succeeded);
            Git(source, "branch", conflictLocal.Value!.BranchName, baseSha);
            typeof(Project).GetProperty(nameof(Project.RepositoryUrl), BindingFlags.Instance | BindingFlags.Public)!.SetValue(project, "https://github.com/owner/repository");
            var conflictPush = await new TaskDeliveryPushService(new ProjectRepository(project), conflictRepository, process, options).PushAsync(conflictDelivery, default);
            Assert.False(conflictPush.Succeeded);
            Assert.Equal("delivery_remote_branch_conflict", conflictPush.Code);
            Assert.Equal(TaskDeliveryStatus.Committed, conflictDelivery.Status);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
                    File.SetAttributes(path, FileAttributes.Normal);
                Directory.Delete(root, true);
            }
        }
    }

    private static ModelSelectionEvidence Evidence() => new(Guid.NewGuid(), "AutomaticRouting", 1, "test", "v1", "[]");
    private static string Git(string cwd, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(error);
        return output;
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
        public int SaveCount
        {
            get; private set;
        }
        public Task<TaskDelivery?> GetByTaskAsync(Guid p, Guid r, Guid t, CancellationToken ct) => Task.FromResult<TaskDelivery?>(delivery); public Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<IReadOnlyList<TaskDelivery>>([delivery]); public Task AddAsync(TaskDelivery d, CancellationToken ct) => Task.CompletedTask; public Task<TaskDelivery?> ClaimNextPendingAsync(Guid id, string owner, DateTimeOffset at, DateTimeOffset expires, CancellationToken ct) => Task.FromResult<TaskDelivery?>(delivery); public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
    private sealed class RunDeliveryRepository : IRunDeliveryRepository
    {
        private readonly RunDelivery delivery;
        public RunDeliveryRepository(TaskDelivery task, string head)
        {
            delivery = RunDelivery.Create(task.ProjectId, task.PipelineRunId, "main", head, "main");
            delivery.StartPreparing();
            delivery.RecordRunBranch(head);
            delivery.StartTaskIntegration();
        }
        public Task<RunDelivery?> GetByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<RunDelivery?>(delivery);
        public Task AddAsync(RunDelivery value, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
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
