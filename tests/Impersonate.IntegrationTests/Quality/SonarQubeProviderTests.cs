using System.Net;
using System.Text;
using Impersonate.Application.Quality;
using Impersonate.Infrastructure.Quality;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
namespace Impersonate.IntegrationTests.Quality;

public sealed class SonarQubeProviderTests
{
    [Fact]
    public async Task Sends_bearer_token_encodes_key_and_parses_metrics()
    {
        HttpRequestMessage? sent = null;
        var handler = new Handler(r => { sent = r; return Json(HttpStatusCode.OK, """{"component":{"measures":[{"metric":"alert_status","value":"OK"},{"metric":"coverage","value":"87.5"},{"metric":"security_rating","value":"1"}]}}"""); });
        var result = await Provider(handler).GetSummaryAsync(new(new("https://sonar.example/"), "project key&branch", "secret-token"), default);
        Assert.True(result.Succeeded);
        Assert.Equal("Bearer", sent!.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", sent.Headers.Authorization.Parameter);
        Assert.Contains("component=project%20key%26branch", sent.RequestUri!.Query);
        Assert.Equal(87.5m, result.Summary!.Coverage.Value);
        Assert.Equal("A", result.Summary.Security.Rating);
    }
    [Fact]
    public async Task Missing_gate_and_malformed_metrics_remain_available()
    {
        var result = await Provider(new Handler(_ => Json(HttpStatusCode.OK, """{"component":{"measures":[{"metric":"coverage","value":"not-a-number"}]}}"""))).GetSummaryAsync(new(new("https://sonar.example/"), "key", "token"), default);
        Assert.True(result.Succeeded);
        Assert.Equal(ProjectQualityState.Available, result.Summary!.State);
        Assert.Null(result.Summary.Coverage.Value);
        Assert.Null(result.Summary.NewCoverage.Value);
    }
    [Theory]
    [InlineData(401, ProjectQualityState.AuthenticationRequired)]
    [InlineData(403, ProjectQualityState.AuthenticationRequired)]
    [InlineData(404, ProjectQualityState.ProjectNotFound)]
    [InlineData(429, ProjectQualityState.TemporarilyUnavailable)]
    [InlineData(500, ProjectQualityState.TemporarilyUnavailable)]
    public async Task Classifies_safe_failures(int status, ProjectQualityState state)
    {
        var result = await Provider(new Handler(_ => new((HttpStatusCode)status))).GetSummaryAsync(new(new("https://sonar.example/"), "key", "token"), default);
        Assert.False(result.Succeeded);
        Assert.Equal(state, result.FailureState);
    }
    [Fact]
    public async Task Rejects_redirects()
    {
        var result = await Provider(new Handler(_ => new(HttpStatusCode.Redirect) { Headers = { Location = new("https://unexpected.example/") } })).GetSummaryAsync(new(new("https://sonar.example/"), "key", "token"), default);
        Assert.Equal("quality_redirect_rejected", result.FailureCode);
    }
    [Fact]
    public async Task Endpoint_policy_rejects_private_targets_and_allows_explicit_local_development()
    {
        var production = new SonarQubeEndpointPolicy(Options.Create(new SonarQubeOptions()), new Environment("Production"));
        Assert.False((await production.ValidateAsync(new("https://127.0.0.1"), default)).Allowed);
        var development = new SonarQubeEndpointPolicy(Options.Create(new SonarQubeOptions { AllowHttpLocalDevelopment = true }), new Environment("Development"));
        Assert.True((await development.ValidateAsync(new("http://localhost:9000"), default)).Allowed);
    }
    private static SonarQubeProvider Provider(HttpMessageHandler handler) => new(new HttpClient(handler), new AllowPolicy());
    private static HttpResponseMessage Json(HttpStatusCode status, string value) => new(status) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(send(request));
    }
    private sealed class AllowPolicy : ISonarQubeEndpointPolicy
    {
        public Task<(bool Allowed, string? Code, string? Message)> ValidateAsync(Uri uri, CancellationToken ct) => Task.FromResult<(bool, string?, string?)>((true, null, null));
    }
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name; public string ApplicationName { get; set; } = "Tests"; public string ContentRootPath { get; set; } = "."; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
