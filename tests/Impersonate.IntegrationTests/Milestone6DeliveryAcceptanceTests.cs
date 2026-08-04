using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Delivery;
using Impersonate.Application.Execution;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Projects;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Delivery;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;
using Impersonate.Infrastructure.Delivery;
using Impersonate.Infrastructure.Delivery.Mcp;
using Impersonate.Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class Milestone6DeliveryAcceptanceTests
{
    [Fact]
    public async Task Two_dependent_tasks_complete_as_two_recoverable_deliveries()
    {
        var root = Path.Combine(Path.GetTempPath(), "impersonate-m6-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source");
            var remote = Path.Combine(root, "remote.git");
            var admin = Path.Combine(root, "admin");
            Directory.CreateDirectory(source);
            Git(source, "init", "-b", "main");
            Git(source, "config", "user.name", "Test");
            Git(source, "config", "user.email", "test@example.invalid");
            await File.WriteAllTextAsync(Path.Combine(source, "README.md"), "base\n");
            await File.WriteAllTextAsync(Path.Combine(source, "P1.txt"), "before one\n");
            await File.WriteAllTextAsync(Path.Combine(source, "P2.txt"), "before two\n");
            Git(source, "add", ".");
            Git(source, "commit", "-m", "initial");
            var baseSha = Git(source, "rev-parse", "HEAD").Trim();
            Git(root, "init", "--bare", remote);
            Git(source, "remote", "add", "origin", remote);
            Git(source, "push", "-u", "origin", "main");
            Git(root, $"--git-dir={remote}", "symbolic-ref", "HEAD", "refs/heads/main");
            Git(root, "clone", remote, admin);
            Git(admin, "config", "user.name", "Acceptance");
            Git(admin, "config", "user.email", "acceptance@example.invalid");
            var patch1 = await PatchAsync(source, "P1.txt", "after one\n");
            var patch2 = await PatchAsync(source, "P2.txt", "after two\n");

            var project = Project.Create("Acceptance", null, "https://github.com/owner/acceptance", "main");
            SetRepository(project, remote);
            var projectRepository = new Projects(project);
            var run = PipelineRun.Create(project.Id, "two incremental tasks");
            run.StartPlanning();
            var task1 = run.AddTask(1, "Add P1", "Add the first file", ["P1 exists"]);
            var task2 = run.AddTask(2, "Add P2", "Add only the second file", ["P2 exists"]);
            task1.SetIntelligence([], [], "Code", "Low", "Low", "first", [], 1, false, null, false);
            task2.SetIntelligence([task1.Id], [], "Code", "Low", "Low", "second", [], 2, false, null, false);
            run.MarkReadyForExecution();
            run.StartExecution();
            var routing = new Routing();
            Approve(run, task1, routing, "artifact:p1", patch1, baseSha);
            Approve(run, task2, routing, "artifact:p2", patch2, baseSha);
            Assert.Equal(PipelineRunStatus.ReadyForDelivery, run.Status);
            Assert.Equal(LoopStage.Committing, run.LoopRun.CurrentStage);
            var runs = new Runs(run);
            var deliveries = new Deliveries(run);
            var coordinator = new TaskDeliveryCoordinator(runs, deliveries, routing);
            var eligibility = await coordinator.GetEligibilityAsync(project.Id, run.Id, default);
            Assert.True(eligibility[0].Eligible);
            Assert.False(eligibility[1].Eligible);

            var d1 = (await coordinator.GetOrCreateAsync(project.Id, run.Id, task1.Id, default)).Value!;
            Assert.Null((await coordinator.GetOrCreateAsync(project.Id, run.Id, task2.Id, default)).Value);
            var options = Options.Create(new ExecutionOptions { DeliveryRoot = Path.Combine(root, "delivery"), CommandTimeoutSeconds = 30 });
            var process = new SafeProcess(new EnvironmentBuilder(), NullLogger<SafeProcess>.Instance);
            var registry = new DeliveryWorkspaceRegistry();
            var artifacts = new Artifacts(new()
            {
                ["artifact:p1"] = patch1,
                ["artifact:p2"] = patch2
            });
            var local = new LocalTargetRepositoryDeliveryService(projectRepository, deliveries, artifacts, new Validation(), registry, process, options);
            var h1 = (await coordinator.BuildHandoffAsync(project.Id, run.Id, task1.Id, default)).Value!;
            var c1 = await local.DeliverApprovedPatchAsync(d1, h1, default);
            Assert.True(c1.Succeeded, $"{c1.Code}: {c1.Error}");
            Assert.Equal(baseSha, d1.DeliveryBaseCommitSha);
            Assert.Equal("1", Git(root, $"--git-dir={Path.Combine(root, "delivery", "repositories", project.Id.ToString("N"), "repository.git")}", "rev-list", "--count", $"{baseSha}..{d1.CommitSha}").Trim());
            SetRepository(project, "https://github.com/owner/acceptance");
            var push = new TaskDeliveryPushService(projectRepository, deliveries, process, options);
            Assert.True((await push.PushAsync(d1, default)).Succeeded);
            Assert.True((await push.PushAsync(d1, default)).Value!.Recovered);
            var mcp = new FakeMcp(remote);
            var gateway = new GitHubMcpPullRequestGateway(projectRepository, mcp, Options.Create(new GitHubMcpOptions { Enabled = true, AllowedRepositories = ["owner/acceptance"] }));
            var pr1 = (await gateway.OpenAsync(d1, h1, default)).Value!;
            Assert.Equal(pr1.Number, (await gateway.OpenAsync(d1, h1, default)).Value!.Number);
            d1.RecordPullRequestOpen(pr1.Provider, pr1.Repository, pr1.Number, pr1.SafeUrl, pr1.HeadBranch, pr1.BaseBranch, pr1.ObservedHeadSha, pr1.CreatedAtUtc);
            d1.AwaitMerge();
            var reconciler = new TaskDeliveryReconciler(deliveries, runs, gateway);
            await reconciler.ProcessOneAsync("reconciler", default);
            Assert.Equal(TaskDeliveryStatus.AwaitingMerge, d1.Status);
            Assert.False((await coordinator.GetEligibilityAsync(project.Id, run.Id, default))[1].Eligible);

            mcp.Merge(pr1.Number);
            Git(admin, "fetch", "origin", d1.RemoteBranchName!);
            Git(admin, "merge", "--ff-only", $"origin/{d1.RemoteBranchName}");
            Git(admin, "push", "origin", "main");
            await reconciler.ProcessOneAsync("reconciler", default);
            Assert.Equal(TaskDeliveryStatus.Merged, d1.Status);
            Assert.True((await coordinator.GetEligibilityAsync(project.Id, run.Id, default))[1].Eligible);
            var d2 = (await coordinator.GetOrCreateAsync(project.Id, run.Id, task2.Id, default)).Value!;
            SetRepository(project, remote);
            var h2 = (await coordinator.BuildHandoffAsync(project.Id, run.Id, task2.Id, default)).Value!;
            var c2 = await local.DeliverApprovedPatchAsync(d2, h2, default);
            Assert.True(c2.Succeeded, c2.Error);
            Assert.Equal(d1.CommitSha, d2.DeliveryBaseCommitSha);
            Assert.Equal<string>(["P2.txt"], Lines(Git(root, $"--git-dir={Path.Combine(root, "delivery", "repositories", project.Id.ToString("N"), "repository.git")}", "diff-tree", "--no-commit-id", "--name-only", "-r", d2.CommitSha!)));
            SetRepository(project, "https://github.com/owner/acceptance");
            Assert.True((await push.PushAsync(d2, default)).Succeeded);
            var pr2 = (await gateway.OpenAsync(d2, h2, default)).Value!;
            d2.RecordPullRequestOpen(pr2.Provider, pr2.Repository, pr2.Number, pr2.SafeUrl, pr2.HeadBranch, pr2.BaseBranch, pr2.ObservedHeadSha, pr2.CreatedAtUtc);
            d2.AwaitMerge();
            Assert.NotEqual(d1.BranchName, d2.BranchName);
            Assert.NotEqual(d1.CommitSha, d2.CommitSha);
            Assert.NotEqual(pr1.Number, pr2.Number);
            mcp.Merge(pr2.Number);
            await reconciler.ProcessOneAsync("reconciler", default);
            Assert.Equal(TaskDeliveryStatus.Merged, d2.Status);
            Assert.Equal(PipelineRunStatus.Completed, run.Status);
            Assert.Equal(2, mcp.CreatedCount);
            Assert.Equal(baseSha, Git(source, "rev-parse", "HEAD").Trim());
            Assert.Equal<string>(["main"], Lines(Git(source, "branch", "--format=%(refname:short)")));
            Assert.DoesNotContain(deliveries.Items, x => x.BranchName?.StartsWith("run/", StringComparison.Ordinal) == true);
        }
        finally { if (Directory.Exists(root)) { foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal); Directory.Delete(root, true); } }
    }

    private static async Task<string> PatchAsync(string repository, string file, string content)
    {
        var path = Path.Combine(repository, file);
        var before = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, content);
        var patch = Git(repository, "diff", "--", file);
        await File.WriteAllTextAsync(path, before);
        return patch;
    }
    private static void Approve(PipelineRun run, PlannedTask task, Routing routing, string artifact, string patch, string baseSha)
    {
        var claimed = run.ClaimNextTask(Guid.NewGuid(), "worker", DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Same(task, claimed);
        var attempt = task.Attempts[^1];
        var sha = Hash(patch);
        attempt.RecordComposition(baseSha, [], "tree", false);
        attempt.RecordExecution("Coder", "coder", "v1", null, 1, 1, 1, JsonSerializer.Serialize(new[] { $"P{task.Sequence}.txt" }), artifact, sha, "[]");
        task.CompleteAttempt("done");
        run.MoveTaskToReview(task);
        var review = run.RecordReview(task, ReviewDecisionType.Approved, "approved");
        review.RecordExecution("Reviewer", "reviewer", "v1", null, 1, 1, sha, "[]");
        routing.Add(run, task, attempt);
        run.FinishApprovedTask(task);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void SetRepository(Project project, string value) => typeof(Project).GetProperty(nameof(Project.RepositoryUrl), BindingFlags.Instance | BindingFlags.Public)!.SetValue(project, value);
    private static IReadOnlyList<string> Lines(string value) => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string Git(string cwd, params string[] args)
    {
        var p = new ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args)
            p.ArgumentList.Add(arg);
        using var process = Process.Start(p)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 && !(args.Contains("--no-index") && process.ExitCode == 1))
            throw new InvalidOperationException(error);
        return output;
    }

    private sealed class Projects(Project project) : IProjectRepository
    {
        public Task<Project?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<Project?>(project); public Task AddAsync(Project p, CancellationToken ct) => Task.CompletedTask; public Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? s, string? q, CancellationToken ct) => Task.FromResult<IReadOnlyList<Project>>([project]); public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Runs(PipelineRun run) : IPipelineRunRepository
    {
        public Task<PipelineRun?> GetAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<PipelineRun?>(run); public Task AddAsync(PipelineRun r, CancellationToken ct) => Task.CompletedTask; public Task<PipelineRun?> ClaimNextExecutionAsync(Guid a, string b, DateTimeOffset c, DateTimeOffset d, CancellationToken ct) => Task.FromResult<PipelineRun?>(null); public Task<IReadOnlyList<PlanningAttempt>> GetPlanningAttemptsAsync(Guid r, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlanningAttempt>>([]); public Task<IReadOnlyList<PipelineRun>> ListAsync(Guid p, PipelineRunStatus? s, DateTimeOffset? f, DateTimeOffset? t, CancellationToken ct) => Task.FromResult<IReadOnlyList<PipelineRun>>([run]); public Task DeleteAsync(Guid p, Guid r, CancellationToken ct) => Task.CompletedTask; public void RemoveTransientAttempt(TaskAttempt a)
        {
        }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Deliveries(PipelineRun run) : ITaskDeliveryRepository
    {
        public List<TaskDelivery> Items { get; } = []; public Task<TaskDelivery?> GetByTaskAsync(Guid p, Guid r, Guid t, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.PlannedTaskId == t)); public Task<IReadOnlyList<TaskDelivery>> ListByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<IReadOnlyList<TaskDelivery>>(Items); public Task AddAsync(TaskDelivery d, CancellationToken ct)
        {
            Items.Add(d);
            ((List<TaskDelivery>)typeof(PipelineRun).GetField("deliveries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run)!).Add(d);
            return Task.CompletedTask;
        }
        public Task<TaskDelivery?> ClaimNextPendingAsync(Guid id, string owner, DateTimeOffset at, DateTimeOffset expires, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.Status is >= TaskDeliveryStatus.Pending and <= TaskDeliveryStatus.Pushed)); public Task<TaskDelivery?> ClaimNextReconciliationAsync(Guid id, string owner, DateTimeOffset at, DateTimeOffset expires, CancellationToken ct)
        {
            var item = Items.FirstOrDefault(x => x.Status == TaskDeliveryStatus.AwaitingMerge);
            item?.Claim(id, owner, expires, at);
            return Task.FromResult(item);
        }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Artifacts(Dictionary<string, string> values) : IExecutionArtifactStore
    {
        public Task<string> ReadTextAsync(string reference, int max, CancellationToken ct) => Task.FromResult(values[reference]); public Task<StoredArtifact> WriteTextAsync(ArtifactScope s, string n, string c, string m, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Validation : IDeliveryValidationService
    {
        public Task<DeliveryOperationResult<IReadOnlyList<DeliveryValidationStep>>> ValidateAsync(DeliveryWorkspaceReference w, CancellationToken ct) => Task.FromResult(DeliveryOperationResult<IReadOnlyList<DeliveryValidationStep>>.Ok([new("acceptance", true, "passed")]));
    }
    private sealed class EnvironmentBuilder : IChildProcessEnvironmentBuilder
    {
        public IReadOnlyDictionary<string, string> Build() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = Environment.GetEnvironmentVariable("PATH")!, ["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot")! };
    }
    private sealed class Routing : IAiRoutingRepository
    {
        private readonly List<ModelSelectionDecision> values = []; public void Add(PipelineRun r, PlannedTask t, TaskAttempt a)
        {
            values.Add(ModelSelectionDecision.Create(r.ProjectId, r.Id, AgentRole.Coder, null, null, "Coder", "coder", ModelSelectionSource.AutomaticRouting, 1, "{}", "coder", "[]", plannedTaskId: t.Id, taskAttemptId: a.Id));
            values.Add(ModelSelectionDecision.Create(r.ProjectId, r.Id, AgentRole.Reviewer, null, null, "Reviewer", "reviewer", ModelSelectionSource.AutomaticRouting, 1, "{}", "reviewer", "[]", plannedTaskId: t.Id, taskAttemptId: a.Id));
        }
        public Task<ModelSelectionDecision?> GetDecisionAsync(Guid p, Guid r, Guid a, AgentRole role, CancellationToken ct) => Task.FromResult(values.LastOrDefault(x => x.TaskAttemptId == a && x.Role == role)); public Task<ModelSelectionDecision?> GetDecisionAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<ModelSelectionDecision?>(null); public Task<IReadOnlyList<AiProviderConnection>> GetConnectionsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AiProviderConnection>>([]); public Task<AiProviderConnection?> GetConnectionAsync(Guid id, CancellationToken ct) => Task.FromResult<AiProviderConnection?>(null); public Task<IReadOnlyList<DiscoveredModel>> GetModelsAsync(Guid? id, CancellationToken ct) => Task.FromResult<IReadOnlyList<DiscoveredModel>>([]); public Task<ProjectAiRoutingPolicy?> GetPolicyAsync(Guid id, CancellationToken ct) => Task.FromResult<ProjectAiRoutingPolicy?>(null); public Task AddConnectionAsync(AiProviderConnection x, CancellationToken ct) => Task.CompletedTask; public Task AddModelAsync(DiscoveredModel x, CancellationToken ct) => Task.CompletedTask; public Task RemoveConnectionAsync(AiProviderConnection x, CancellationToken ct) => Task.CompletedTask; public Task<ProjectAiRoutingPolicy> GetOrCreatePolicyAsync(Guid id, CancellationToken ct) => Task.FromResult(ProjectAiRoutingPolicy.Create(id)); public Task AddDecisionAsync(ModelSelectionDecision x, CancellationToken ct) => Task.CompletedTask; public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FakeMcp(string remote) : IGitHubMcpClient
    {
        private readonly Dictionary<long, Pr> prs = []; private long next; public int CreatedCount => prs.Count; public string ServerIdentity => "acceptance-fake-official"; public void Merge(long number) => prs[number] = prs[number] with { State = "closed", Merged = true, MergeSha = "merged" }; public Task<JsonElement> CallToolAsync(string tool, object arguments, CancellationToken ct)
        {
            var a = JsonSerializer.SerializeToElement(arguments);
            if (tool == "list_pull_requests")
                return Task.FromResult(JsonSerializer.SerializeToElement(prs.Values.Where(x => x.Head == a.GetProperty("head").GetString()!.Split(':')[1]).Select(Value)));
            if (tool == "create_pull_request")
            {
                var number = ++next;
                prs[number] = new(number, a.GetProperty("head").GetString()!, a.GetProperty("base").GetString()!, Git(Path.GetDirectoryName(remote)!, $"--git-dir={remote}", "rev-parse", a.GetProperty("head").GetString()!).Trim(), "open", false, null);
                return Task.FromResult(JsonSerializer.SerializeToElement(new
                {
                    number
                }));
            }
            var pr = prs[a.GetProperty("pullNumber").GetInt64()];
            return Task.FromResult(JsonSerializer.SerializeToElement(Value(pr)));
        }
        private static object Value(Pr p) => new { number = p.Number, html_url = $"https://github.com/owner/acceptance/pull/{p.Number}", state = p.State, merged = p.Merged, head = new { @ref = p.Head, sha = p.Sha }, @base = new { @ref = p.Base }, created_at = "2026-08-04T00:00:00Z", merge_commit_sha = p.MergeSha }; private sealed record Pr(long Number, string Head, string Base, string Sha, string State, bool Merged, string? MergeSha);
    }
}
