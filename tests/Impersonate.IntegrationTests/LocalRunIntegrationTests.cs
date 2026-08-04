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

public sealed class LocalRunIntegrationTests
{
    [Fact]
    public async Task One_run_branch_is_created_once_and_replayed_without_force_push()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            Git(source, "init", "-b", "main");
            Git(source, "config", "user.name", "Test");
            Git(source, "config", "user.email", "test@example.com");
            await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "base\n");
            Git(source, "add", ".");
            Git(source, "commit", "-m", "base");
            var head = Git(source, "rev-parse", "HEAD").Trim();
            var project = Project.Create("Test", null, "https://github.com/owner/repo", "main");
            typeof(Project).GetProperty(nameof(Project.RepositoryUrl))!.SetValue(project, source);
            var delivery = RunDelivery.Create(project.Id, Guid.NewGuid(), "main", head, "impersonate/run-test-feature");
            var repository = new Repository(delivery);
            var service = new LocalRunIntegrationService(new Projects(project), repository, new SafeProcess(new EnvironmentBuilder(), NullLogger<SafeProcess>.Instance), Options.Create(new ExecutionOptions { DeliveryRoot = Path.Combine(root, "delivery"), CommandTimeoutSeconds = 30 }));
            var first = await service.PrepareAsync(delivery, default);
            var replay = await service.PrepareAsync(delivery, default);
            Assert.True(first.Succeeded, first.Error);
            Assert.True(replay.Succeeded, replay.Error);
            Assert.Equal(RunDeliveryStatus.IntegratingTasks, delivery.Status);
            Assert.Equal(head, delivery.RunBranchHeadSha);
            Assert.Equal(head, Git(source, "rev-parse", delivery.RunBranchName).Trim());
            Assert.True(repository.SaveCount >= 3);
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

    private static string Git(string cwd, params string[] args)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(error);
        return output;
    }
    private sealed class Projects(Project project) : IProjectRepository
    {
        public Task<Project?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<Project?>(project); public Task AddAsync(Project value, CancellationToken ct) => Task.CompletedTask; public Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, string? search, CancellationToken ct) => Task.FromResult<IReadOnlyList<Project>>([project]); public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Repository(RunDelivery delivery) : IRunDeliveryRepository
    {
        public int SaveCount
        {
            get; private set;
        }
        public Task<RunDelivery?> GetByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<RunDelivery?>(delivery); public Task AddAsync(RunDelivery value, CancellationToken ct) => Task.CompletedTask; public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
    private sealed class EnvironmentBuilder : IChildProcessEnvironmentBuilder
    {
        public IReadOnlyDictionary<string, string> Build() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = Environment.GetEnvironmentVariable("PATH")!, ["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot")! };
    }
}
