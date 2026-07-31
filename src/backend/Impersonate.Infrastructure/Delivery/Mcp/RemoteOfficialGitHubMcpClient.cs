using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery.Mcp;

internal sealed class RemoteOfficialGitHubMcpClient(HttpClient http, IOptions<GitHubMcpOptions> configured) : IGitHubMcpClient
{
    private readonly GitHubMcpOptions options = configured.Value;
    private readonly SemaphoreSlim initializeGate = new(1, 1);
    private string? sessionId;
    private long nextId;
    public string ServerIdentity => options.ServerId;

    public async Task<JsonElement> CallToolAsync(string tool, object arguments, CancellationToken ct)
    {
        EnsureAllowed(tool);
        await InitializeAsync(ct);
        return await SendAsync("tools/call", new { name = tool, arguments }, ct);
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        if (sessionId is not null) return;
        await initializeGate.WaitAsync(ct);
        try
        {
            if (sessionId is not null) return;
            await SendAsync("initialize", new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "Impersonate", version = "1.0" } }, ct, initialize: true);
            using var request = Request(new { jsonrpc = "2.0", method = "notifications/initialized", @params = new { } });
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("github_mcp_initialization_failed");
        }
        finally { initializeGate.Release(); }
    }

    private async Task<JsonElement> SendAsync(string method, object parameters, CancellationToken ct, bool initialize = false)
    {
        var id = Interlocked.Increment(ref nextId);
        using var request = Request(new { jsonrpc = "2.0", id, method, @params = parameters });
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "github_mcp_authentication_unavailable" : "github_mcp_unavailable");
        if (initialize && response.Headers.TryGetValues("Mcp-Session-Id", out var values)) sessionId = values.SingleOrDefault();
        var payload = await response.Content.ReadAsStringAsync(ct);
        return McpJson.Result(payload, id);
    }

    private HttpRequestMessage Request(object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, options.RemoteUrl) { Content = JsonContent.Create(body) };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("X-MCP-Tools", string.Join(',', options.Tools));
        request.Headers.TryAddWithoutValidation("X-MCP-Readonly", "false");
        if (sessionId is not null) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
        var token = Environment.GetEnvironmentVariable(options.TokenEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new("Bearer", token);
        return request;
    }
    private void EnsureAllowed(string tool)
    {
        if (!options.Enabled || !options.Tools.Contains(tool, StringComparer.Ordinal) || options.Tools.Except(["list_pull_requests", "pull_request_read", "create_pull_request"], StringComparer.Ordinal).Any()) throw new InvalidOperationException("github_mcp_tool_not_allowed");
        if (!Uri.TryCreate(options.RemoteUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "api.githubcopilot.com", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("github_mcp_server_not_allowed");
    }
}
