using System.Net;
using System.Text;
using System.Text.Json;
using Impersonate.Application.Delivery;
using Impersonate.Application.Projects;
using Impersonate.Domain.Delivery;
using Impersonate.Domain.Projects;
using Impersonate.Infrastructure.Delivery.Mcp;
using Microsoft.Extensions.Options;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class GitHubMcpPullRequestGatewayTests
{
    [Fact]
    public async Task Creates_one_draft_pr_with_safe_evidence_and_exact_tools()
    {
        var fixture = new Fixture();
        fixture.Mcp.Results.Enqueue(Json(Array.Empty<object>()));
        fixture.Mcp.Results.Enqueue(Json(new
        {
            number = 17
        }));
        fixture.Mcp.Results.Enqueue(Pr(17, fixture.Delivery.CommitSha!));
        var result = await fixture.Gateway.OpenAsync(fixture.Delivery, fixture.Handoff, default);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(["list_pull_requests", "create_pull_request", "pull_request_read"], fixture.Mcp.Calls);
        var create = fixture.Mcp.Arguments[1];
        Assert.True(create.GetProperty("draft").GetBoolean());
        Assert.Equal("impersonate/run-test", create.GetProperty("base").GetString());
        Assert.NotEqual(fixture.Project.DefaultBranch, create.GetProperty("base").GetString());
        var body = create.GetProperty("body").GetString()!;
        Assert.Contains("impersonate/run-test", body);
        Assert.Contains(fixture.Handoff.ApprovedPatchSha256, body);
        Assert.DoesNotContain(fixture.Handoff.ApprovedPatchArtifactReference, body);
    }

    [Fact]
    public async Task Matching_existing_pr_is_reused_and_conflicting_head_blocks()
    {
        var matching = new Fixture();
        matching.Mcp.Results.Enqueue(Json(new[] { PrObject(4, matching.Delivery.CommitSha!) }));
        var reused = await matching.Gateway.OpenAsync(matching.Delivery, matching.Handoff, default);
        Assert.True(reused.Succeeded);
        Assert.Equal(4, reused.Value!.Number);
        Assert.Equal(["list_pull_requests"], matching.Mcp.Calls);

        var conflict = new Fixture();
        conflict.Mcp.Results.Enqueue(Json(new[] { PrObject(5, "different") }));
        var blocked = await conflict.Gateway.OpenAsync(conflict.Delivery, conflict.Handoff, default);
        Assert.False(blocked.Succeeded);
        Assert.Equal("delivery_pull_request_head_changed", blocked.Code);
    }

    [Fact]
    public async Task Lost_create_response_recovers_and_closed_pr_is_not_replaced()
    {
        var lost = new Fixture();
        lost.Mcp.Results.Enqueue(Json(Array.Empty<object>()));
        lost.Mcp.FailOnCall = 2;
        lost.Mcp.Results.Enqueue(Json(new[] { PrObject(8, lost.Delivery.CommitSha!) }));
        var recovered = await lost.Gateway.OpenAsync(lost.Delivery, lost.Handoff, default);
        Assert.True(recovered.Succeeded);
        Assert.Equal(8, recovered.Value!.Number);

        var closed = new Fixture();
        closed.Mcp.Results.Enqueue(Json(new[] { PrObject(9, closed.Delivery.CommitSha!, "closed") }));
        var blocked = await closed.Gateway.OpenAsync(closed.Delivery, closed.Handoff, default);
        Assert.False(blocked.Succeeded);
        Assert.Equal("delivery_pull_request_closed", blocked.Code);
    }

    [Fact]
    public async Task Remote_protocol_uses_write_mode_and_only_allowlisted_tool()
    {
        var handler = new FakeMcpHandler();
        var options = Options.Create(OptionsValue());
        var client = new RemoteOfficialGitHubMcpClient(new HttpClient(handler), options);
        var result = await client.CallToolAsync("list_pull_requests", new
        {
            owner = "owner",
            repo = "repo"
        }, default);
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.Equal(["initialize", "notifications/initialized", "tools/call"], handler.Methods);
        Assert.All(handler.ToolsHeaders, value => Assert.Equal("list_pull_requests,pull_request_read,create_pull_request", value));
        Assert.All(handler.ReadOnlyHeaders, value => Assert.Equal("false", value));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CallToolAsync("merge_pull_request", new { }, default));
        Assert.Equal(3, handler.Methods.Count);
    }

    [Fact]
    public async Task Malformed_list_response_fails_without_creating_a_pr()
    {
        var fixture = new Fixture();
        fixture.Mcp.Results.Enqueue(Json(new
        {
            unexpected = true
        }));
        var result = await fixture.Gateway.OpenAsync(fixture.Delivery, fixture.Handoff, default);
        Assert.False(result.Succeeded);
        Assert.Equal("github_mcp_malformed_response", result.Code);
        Assert.Equal(["list_pull_requests"], fixture.Mcp.Calls);
    }

    [Theory]
    [InlineData("open", false, PullRequestExternalState.Open)]
    [InlineData("closed", false, PullRequestExternalState.Closed)]
    [InlineData("closed", true, PullRequestExternalState.Merged)]
    public async Task Reconciliation_reads_exact_pr_state(string state, bool merged, PullRequestExternalState expected)
    {
        var fixture = new Fixture();
        fixture.Delivery.RecordPullRequestOpen("GitHubMCP:fake-official", "owner/repo", 12, "https://github.com/owner/repo/pull/12", "impersonate/task", "impersonate/run-test", fixture.Delivery.CommitSha!, DateTimeOffset.UtcNow);
        fixture.Delivery.StartDeliveryReview();
        fixture.Mcp.Results.Enqueue(Json(PrObject(12, fixture.Delivery.CommitSha!, state, merged)));
        var result = await fixture.Gateway.ReadAsync(fixture.Delivery, default);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(expected, result.Value!.State);
        Assert.Equal(["pull_request_read"], fixture.Mcp.Calls);
    }

    [Fact]
    public async Task Reconciliation_blocks_changed_head_identity()
    {
        var fixture = new Fixture();
        fixture.Delivery.RecordPullRequestOpen("GitHubMCP:fake-official", "owner/repo", 12, "https://github.com/owner/repo/pull/12", "impersonate/task", "impersonate/run-test", fixture.Delivery.CommitSha!, DateTimeOffset.UtcNow);
        fixture.Delivery.StartDeliveryReview();
        fixture.Mcp.Results.Enqueue(Json(PrObject(12, "unapproved")));
        var result = await fixture.Gateway.ReadAsync(fixture.Delivery, default);
        Assert.False(result.Succeeded);
        Assert.Equal("delivery_pull_request_head_changed", result.Code);
    }

    private sealed class Fixture
    {
        public Project Project { get; } = Project.Create("Test", null, "https://github.com/owner/repo", "main");
        public TaskDelivery Delivery
        {
            get;
        }
        public ApprovedTaskHandoff Handoff
        {
            get;
        }
        public FakeMcpClient Mcp { get; } = new();
        public RunDelivery Aggregate
        {
            get;
        }
        public GitHubMcpPullRequestGateway Gateway
        {
            get;
        }
        public Fixture()
        {
            Delivery = TaskDelivery.Create(Project.Id, Guid.NewGuid(), Guid.NewGuid(), 1, "base", "artifact:secret-patch", "patch-sha", Guid.NewGuid());
            Delivery.StartPreparing();
            Delivery.RecordDeliveryBase("base");
            Delivery.RecordBranchIntent("impersonate/task");
            Delivery.RecordBranchPrepared("impersonate/task");
            Delivery.RecordPatchApplied();
            Delivery.RecordValidated("[]");
            Delivery.RecordCommitted("commit-sha");
            Delivery.RecordPushed("origin", "owner/repo", "impersonate/task", "commit-sha");
            Aggregate = RunDelivery.Create(Project.Id, Delivery.PipelineRunId, "main", "base", "impersonate/run-test");
            Aggregate.StartPreparing();
            Aggregate.RecordRunBranch("base");
            Aggregate.StartTaskIntegration();
            Handoff = new(Project.Id, Delivery.PipelineRunId, Delivery.PlannedTaskId, 1, "Focused task", "Description", ["It works"], [], "base", "artifact:secret-patch", "patch-sha", ["src/file.cs"], [], Delivery.ApprovedReviewDecisionId, "reviewer", "review-model", "Approved safely", "coder", "coder-model", Evidence(), Evidence(), Guid.NewGuid(), 1, 0);
            Gateway = new(new ProjectRepository(Project), new RunDeliveryRepository(Aggregate), Mcp, Options.Create(OptionsValue()));
        }
    }
    private sealed class FakeMcpClient : IGitHubMcpClient
    {
        public string ServerIdentity => "fake-official"; public Queue<JsonElement> Results { get; } = new(); public List<string> Calls { get; } = []; public List<JsonElement> Arguments { get; } = []; public int? FailOnCall
        {
            get; set;
        }
        public Task<JsonElement> CallToolAsync(string tool, object arguments, CancellationToken ct)
        {
            Calls.Add(tool);
            Arguments.Add(JsonSerializer.SerializeToElement(arguments));
            if (FailOnCall == Calls.Count)
                throw new InvalidOperationException("lost");
            return Task.FromResult(Results.Dequeue());
        }
    }
    private sealed class ProjectRepository(Project project) : IProjectRepository
    {
        public Task<Project?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<Project?>(project); public Task AddAsync(Project p, CancellationToken ct) => Task.CompletedTask; public Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, string? search, CancellationToken ct) => Task.FromResult<IReadOnlyList<Project>>([project]); public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class RunDeliveryRepository(RunDelivery delivery) : IRunDeliveryRepository
    {
        public Task<RunDelivery?> GetByRunAsync(Guid p, Guid r, CancellationToken ct) => Task.FromResult<RunDelivery?>(delivery);
        public Task AddAsync(RunDelivery value, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FakeMcpHandler : HttpMessageHandler
    {
        public List<string> Methods { get; } = []; public List<string> ToolsHeaders { get; } = []; public List<string> ReadOnlyHeaders { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ToolsHeaders.Add(request.Headers.GetValues("X-MCP-Tools").Single());
            ReadOnlyHeaders.Add(request.Headers.GetValues("X-MCP-Readonly").Single());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var method = body.RootElement.GetProperty("method").GetString()!;
            Methods.Add(method);
            if (method == "notifications/initialized")
                return new(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("")
                };
            var id = body.RootElement.GetProperty("id").GetInt64();
            object result = method == "initialize" ? new
            {
                protocolVersion = "2025-06-18"
            } : new
            {
                structuredContent = Array.Empty<object>()
            };
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id,
                    result
                }), Encoding.UTF8, "application/json")
            };
        }
    }
    private static GitHubMcpOptions OptionsValue() => new() { Enabled = true, AllowedRepositories = ["owner/repo"] };
    private static ModelSelectionEvidence Evidence() => new(Guid.NewGuid(), "AutomaticRouting", 1, "test", "v1", "[]");
    private static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);
    private static JsonElement Pr(long number, string sha) => Json(PrObject(number, sha));
    private static object PrObject(long number, string sha, string state = "open", bool merged = false) => new { number, html_url = $"https://github.com/owner/repo/pull/{number}", state, merged, head = new { @ref = "impersonate/task", sha }, @base = new { @ref = "impersonate/run-test" }, created_at = "2026-07-31T00:00:00Z", merge_commit_sha = merged ? "merge-sha" : null };
}
