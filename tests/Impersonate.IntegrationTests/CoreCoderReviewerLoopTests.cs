using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Infrastructure.Agents.Execution;
using Microsoft.Extensions.Options;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class CoreCoderReviewerLoopTests
{
    [Fact]
    public async Task Coder_sends_phase_appropriate_reservations_and_returns_telemetry()
    {
        var run = ExecutableRun(0);
        var task = run.ClaimNextTask(Guid.NewGuid(), "worker", DateTimeOffset.UtcNow.AddMinutes(1));
        var tools = new LoopTools();
        var fixture = CoderWithAdapter(tools);
        var result = await fixture.Agent.ExecuteAsync(Context(run, task, null), default);

        Assert.True(result.Succeeded, result.FailureMessage);
        var reservations = fixture.Adapter.Requests.Select(x => x.MaximumOutputTokens).ToArray();
        Assert.Equal(1_200, reservations[0]);
        Assert.True(reservations[1] >= 3_000);
        Assert.True(reservations[2] < reservations[1]);
        Assert.True(reservations[3] <= reservations[2]);
        Assert.Equal(reservations.Max(), result.MaximumRequestedOutputReservation);
        Assert.Equal(4, result.OutputReservationReasons!.Count);
        Assert.Equal(20, result.OutputTokenCount);
    }

    [Fact]
    public async Task Terminal_rate_limit_preserves_prior_usage_and_capacity_telemetry()
    {
        var run = ExecutableRun(0);
        var task = run.ClaimNextTask(Guid.NewGuid(), "worker", DateTimeOffset.UtcNow.AddMinutes(1));
        var agent = new CoderAgent([new RateLimitAfterReadAdapter()], new Credentials(), new LoopTools(), Options.Create(new ExecutionOptions { DefaultModelContextWindowTokens = 32_000 }));
        var result = await agent.ExecuteAsync(Context(run, task, null), default);
        Assert.False(result.Succeeded);
        Assert.Equal("provider_rate_limited", result.FailureCode);
        Assert.Equal(10, result.InputTokenCount);
        Assert.Equal(5, result.OutputTokenCount);
        Assert.Equal(1, result.ProviderRoundTripCount);
        Assert.Equal(1, result.PaidProviderRequestCount);
        Assert.Equal(1, result.ToolStepCount);
        Assert.Equal(250, result.ProviderCapacityWaitMilliseconds);
        Assert.True(result.ProviderResetUsed);
        Assert.Equal("Tokens", result.LastRateLimitScope);
    }

    [Fact]
    public async Task Fake_provider_changes_request_creates_finite_revision_then_approval()
    {
        var run = ExecutableRun(maximumRevisions: 1);
        var task = run.Tasks.Single();
        var tools = new LoopTools();
        var first = run.ClaimNextTask(Guid.NewGuid(), "worker", DateTimeOffset.UtcNow.AddMinutes(1));
        var coded = await Coder(tools).ExecuteAsync(Context(run, first, null), default);
        Assert.True(coded.Succeeded);
        Assert.Equal(1, tools.PatchApplications);
        Assert.False(string.IsNullOrWhiteSpace((await tools.GetDiffAsync(new("workspace"), default)).Output));
        first.CompleteAttempt(coded.Summary);
        run.MoveTaskToReview(first);
        var firstReviewer = Reviewer("ChangesRequested", "Add validation");
        var firstReview = await firstReviewer.Agent.ReviewAsync(ReviewContext(run, first, coded, tools), default);
        Assert.Contains("actualPatch", firstReviewer.Adapter.Requests.Single().UserContent);
        Assert.Contains("sha256", firstReviewer.Adapter.Requests.Single().UserContent);
        var rejected = run.RecordReview(first, firstReview.Decision!.Value, firstReview.Summary, firstReview.Feedback);
        run.ClearExecutionClaim();
        Assert.Equal(PlannedTaskStatus.ChangesRequested, first.Status);
        Assert.True(rejected.IsCurrent);

        var revision = run.ClaimNextTask(Guid.NewGuid(), "worker", DateTimeOffset.UtcNow.AddMinutes(1));
        var revised = await Coder(tools).ExecuteAsync(Context(run, revision, rejected.Feedback), default);
        Assert.True(revised.Succeeded);
        Assert.Equal(2, tools.PatchApplications);
        revision.CompleteAttempt(revised.Summary);
        run.MoveTaskToReview(revision);
        var secondReviewer = Reviewer("Approved", null);
        var secondReview = await secondReviewer.Agent.ReviewAsync(ReviewContext(run, revision, revised, tools), default);
        var approved = run.RecordReview(revision, secondReview.Decision!.Value, secondReview.Summary, secondReview.Feedback);
        run.FinishApprovedTask(revision);

        Assert.False(rejected.IsCurrent);
        Assert.True(approved.IsCurrent);
        Assert.Equal(1, task.RevisionCount);
        Assert.Equal(2, task.Attempts.Count);
        Assert.Equal(PlannedTaskStatus.Approved, task.Status);
        Assert.Equal(PipelineRunStatus.ReadyForDelivery, run.Status);
        Assert.Equal(LoopStage.Committing, run.LoopRun.CurrentStage);
    }

    [Fact]
    public async Task Invalid_fake_reviewer_output_cannot_approve_or_hide_failed_task()
    {
        var run = ExecutableRun(maximumRevisions: 0, continueOnFailure: false);
        var task = run.ClaimNextTask(Guid.NewGuid(), "worker", DateTimeOffset.UtcNow.AddMinutes(1));
        var tools = new LoopTools();
        var coded = await Coder(tools).ExecuteAsync(Context(run, task, null), default);
        task.CompleteAttempt(coded.Summary);
        run.MoveTaskToReview(task);
        var invalidReviewer = ReviewerRaw("{\"decision\":\"Approved\",\"summary\":\"looks good\",\"feedback\":null,\"findings\":[{\"severity\":\"Blocking\",\"message\":\"broken\",\"path\":null,\"line\":null}]}");
        var reviewed = await invalidReviewer.Agent.ReviewAsync(ReviewContext(run, task, coded, tools), default);
        Assert.False(reviewed.Succeeded);
        Assert.Equal("invalid_reviewer_output", reviewed.FailureCode);
        Assert.Empty(task.ReviewDecisions);
        Assert.NotEqual(PlannedTaskStatus.Approved, task.Status);
        run.ResolveExecutionFailure(task, $"{reviewed.FailureCode}: {reviewed.FailureMessage}");
        Assert.Equal(PlannedTaskStatus.Failed, task.Status);
        Assert.Equal(PipelineRunStatus.Failed, run.Status);
        Assert.Empty(task.ReviewDecisions);
    }

    private static PipelineRun ExecutableRun(int maximumRevisions, bool continueOnFailure = true)
    {
        var run = PipelineRun.Create(Guid.NewGuid(), "Add profile", maximumRevisions, continueOnFailure);
        run.StartPlanning();
        run.AddTask(1, "Add profile", "Implement profile", ["Profile works"]);
        run.MarkReadyForExecution();
        run.StartExecution();
        return run;
    }
    private static CoderAgent Coder(LoopTools tools) => CoderWithAdapter(tools).Agent;
    private static (CoderAgent Agent, SequenceAdapter Adapter) CoderWithAdapter(LoopTools tools)
    {
        var adapter = new SequenceAdapter(CoderResponses());
        return (new([adapter], new Credentials(), tools, Options.Create(new ExecutionOptions { MaximumCoderToolExecutions = 5, DefaultModelContextWindowTokens = 32_000 })), adapter);
    }
    private static (ReviewerAgent Agent, SequenceAdapter Adapter) Reviewer(string decision, string? feedback) => ReviewerRaw(JsonSerializer.Serialize(new { decision, summary = "reviewed", feedback, findings = Array.Empty<object>() }));
    private static (ReviewerAgent Agent, SequenceAdapter Adapter) ReviewerRaw(string response)
    {
        var adapter = new SequenceAdapter([response]);
        return (new ReviewerAgent([adapter], new Credentials(), Options.Create(new ExecutionOptions())), adapter);
    }
    private static CoderContext Context(PipelineRun run, PlannedTask task, string? feedback) => new(run.ProjectId, run.Id, run.FeatureRequest, task.Id, task.Title, task.Description, task.AcceptanceCriteria, task.Attempts.Last().AttemptNumber, task.RevisionCount, feedback, [], new("workspace"), Model(), RepositoryEvidence: ["User.cs"]);
    private static ReviewerContext ReviewContext(PipelineRun run, PlannedTask task, CoderResult coded, LoopTools tools) => new(run.ProjectId, run.Id, run.FeatureRequest, task.Id, task.Title, task.Description, task.AcceptanceCriteria, task.Attempts.Last().AttemptNumber, tools.Diff, "sha256", coded.ChangedFiles, coded.ValidationNotes, coded.Summary, null, new("workspace"), Model());
    private static SelectedModel Model() => new(Guid.NewGuid(), Guid.NewGuid(), ProviderType.OpenAI, "gpt-test", ModelSelectionSource.AutomaticRouting, 100, "fixture");
    private static IReadOnlyList<string> CoderResponses() => [
        "{\"type\":\"tool_calls\",\"calls\":[{\"id\":\"read\",\"tool\":\"read_file\",\"arguments\":{\"path\":\"User.cs\",\"query\":null,\"patch\":null,\"executable\":null,\"arguments\":null,\"workingDirectory\":null,\"timeoutSeconds\":null}}],\"summary\":null,\"validationNotes\":null,\"knownLimitations\":null}",
        "{\"type\":\"tool_calls\",\"calls\":[{\"id\":\"patch\",\"tool\":\"apply_patch\",\"arguments\":{\"path\":null,\"query\":null,\"patch\":\"patch\",\"executable\":null,\"arguments\":null,\"workingDirectory\":null,\"timeoutSeconds\":null}}],\"summary\":null,\"validationNotes\":null,\"knownLimitations\":null}",
        "{\"type\":\"tool_calls\",\"calls\":[{\"id\":\"validate\",\"tool\":\"run_command\",\"arguments\":{\"path\":null,\"query\":null,\"patch\":null,\"executable\":\"dotnet\",\"arguments\":[\"test\"],\"workingDirectory\":\".\",\"timeoutSeconds\":120}}],\"summary\":null,\"validationNotes\":null,\"knownLimitations\":null}",
        "{\"type\":\"complete\",\"calls\":null,\"summary\":\"implemented\",\"validationNotes\":[],\"knownLimitations\":[]}"
    ];

    private sealed class SequenceAdapter(IReadOnlyList<string> responses) : IAiProviderAdapter
    {
        private int index; public List<LanguageModelRequest> Requests { get; } = []; public ProviderType ProviderType => ProviderType.OpenAI;
        public Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext connection, RoutedModel model, LanguageModelRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new LanguageModelResponse(responses[index++], $"request-{index}", 10, 5));
        }
        public async Task<AgentTurnResponse> CompleteAgentTurnAsync(ProviderConnectionContext connection, RoutedModel model, AgentTurnRequest request, CancellationToken cancellationToken)
        {
            var response = await CompleteAsync(connection, model, new(request.Model, request.SystemInstructions, request.InitialInput ?? "continuation", "{}", request.MaximumOutputTokens), cancellationToken);
            using var document = JsonDocument.Parse(response.Content);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString();
            var calls = new List<AgentToolCall>();
            if (type == "tool_calls")
                foreach (var call in root.GetProperty("calls").EnumerateArray())
                {
                    var tool = call.GetProperty("tool").GetString()!;
                    var source = call.GetProperty("arguments");
                    var args = tool switch
                    {
                        "read_file" => JsonSerializer.Serialize(new { path = source.GetProperty("path").GetString() }),
                        "apply_patch" => JsonSerializer.Serialize(new { patch = source.GetProperty("patch").GetString() }),
                        "get_diff" => "{}",
                        "run_command" => JsonSerializer.Serialize(new { executable = source.GetProperty("executable").GetString(), arguments = source.GetProperty("arguments").EnumerateArray().Select(x => x.GetString()).ToArray(), workingDirectory = source.GetProperty("workingDirectory").GetString(), timeoutSeconds = source.GetProperty("timeoutSeconds").GetInt32() }),
                        _ => "{}"
                    };
                    calls.Add(new(call.GetProperty("id").GetString()! + "-" + index, tool, args));
                }
            else
                calls.Add(new("terminal-" + index, "complete_task", JsonSerializer.Serialize(new
                {
                    summary = root.GetProperty("summary").GetString(),
                    validationNotes = Array.Empty<string>(),
                    knownLimitations = Array.Empty<string>()
                })));
            return new(new(response.ProviderRequestId!), calls, response.ProviderRequestId, response.InputTokenCount, response.OutputTokenCount, "completed");
        }
        public Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext connection, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext connection, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class Credentials : IProviderCredentialStore
    {
        public Task<ProviderCredentialReadResult> RetrieveAsync(Guid connectionId, CancellationToken cancellationToken) => Task.FromResult(new ProviderCredentialReadResult(ProviderCredentialReadStatus.Found, new("fake"), null, null));
        public Task StoreAsync(Guid connectionId, ProviderCredential credential, CancellationToken cancellationToken) => throw new NotSupportedException(); public Task DeleteAsync(Guid connectionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class RateLimitAfterReadAdapter : IAiProviderAdapter
    {
        private int calls;
        public ProviderType ProviderType => ProviderType.OpenAI;
        public Task<AgentTurnResponse> CompleteAgentTurnAsync(ProviderConnectionContext connection, RoutedModel model, AgentTurnRequest request, CancellationToken cancellationToken)
        {
            if (calls++ == 0)
                return Task.FromResult(new AgentTurnResponse(new("first"), [new("read", "read_file", "{\"path\":\"User.cs\"}")], "first", 10, 5, "completed"));
            var capacity = new ProviderCapacityMetadata(System.Net.HttpStatusCode.TooManyRequests, RetryAfter: TimeSpan.FromSeconds(1), TokenReset: TimeSpan.FromSeconds(1), RemainingTokens: 0, Scope: RateLimitScope.Tokens, TemporaryCapacity: true, CumulativeWaitMilliseconds: 250);
            throw new ProviderRequestException("provider_rate_limited", "Rate limited.", System.Net.HttpStatusCode.TooManyRequests, true, capacity);
        }
        public Task<LanguageModelResponse> CompleteAsync(ProviderConnectionContext connection, RoutedModel model, LanguageModelRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProviderModel>> DiscoverModelsAsync(ProviderConnectionContext connection, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProviderValidationResult> ValidateAsync(ProviderConnectionContext connection, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class LoopTools : IRepositoryTools
    {
        public int PatchApplications
        {
            get; private set;
        }
        public string Diff => PatchApplications == 0 ? string.Empty : $"diff --git a/User.cs b/User.cs\n+revision {PatchApplications}\n";
        public Task<RepositoryToolResult> ReadFileAsync(WorkspaceReference workspace, string relativePath, CancellationToken ct) => Task.FromResult(new RepositoryToolResult(true, "public sealed class User {}"));
        public Task<RepositoryToolResult> ApplyPatchAsync(WorkspaceReference workspace, string patch, CancellationToken ct)
        {
            PatchApplications++;
            return Task.FromResult(new RepositoryToolResult(true, "applied"));
        }
        public Task<RepositoryToolResult> GetDiffAsync(WorkspaceReference workspace, CancellationToken ct) => Task.FromResult(new RepositoryToolResult(true, Diff));
        public Task<RepositoryToolResult> RunCommandAsync(WorkspaceReference workspace, RepositoryCommand command, CancellationToken ct) => Task.FromResult(new RepositoryToolResult(true, "User.cs\n"));
        public Task<RepositoryToolResult> ListFilesAsync(WorkspaceReference workspace, string relativePath, CancellationToken ct) => throw new NotSupportedException(); public Task<RepositoryToolResult> SearchTextAsync(WorkspaceReference workspace, string query, string relativePath, CancellationToken ct) => throw new NotSupportedException();
    }
}
