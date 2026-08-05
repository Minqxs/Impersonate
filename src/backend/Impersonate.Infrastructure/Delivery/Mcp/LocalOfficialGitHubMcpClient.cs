using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery.Mcp;

internal sealed class LocalOfficialGitHubMcpClient(IOptions<GitHubMcpOptions> configured) : IGitHubMcpClient
{
    private readonly GitHubMcpOptions options = configured.Value;
    public string ServerIdentity => options.ServerId;

    public async Task<JsonElement> CallToolAsync(string tool, object arguments, CancellationToken ct)
    {
        EnsureAllowed(tool);
        var file = Path.GetFileNameWithoutExtension(options.LocalCommand);
        if (!string.Equals(file, "github-mcp-server", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("github_mcp_server_not_allowed");
        var start = new ProcessStartInfo(options.LocalCommand) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        if (!options.LocalArguments.SequenceEqual(["stdio"], StringComparer.Ordinal))
            throw new InvalidOperationException("github_mcp_server_not_allowed");
        foreach (var argument in options.LocalArguments)
            start.ArgumentList.Add(argument);
        start.Environment.Clear();
        Copy("PATH");
        Copy("SystemRoot");
        Copy("WINDIR");
        var token = Environment.GetEnvironmentVariable(options.TokenEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(token))
            start.Environment["GITHUB_PERSONAL_ACCESS_TOKEN"] = token;
        start.Environment["GITHUB_TOOLS"] = string.Join(',', options.Tools);
        start.Environment["GITHUB_READ_ONLY"] = "false";
        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch { throw new InvalidOperationException("github_mcp_unavailable"); }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        _ = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await WriteAsync(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-06-18",
                    capabilities = new
                    {
                    },
                    clientInfo = new
                    {
                        name = "Impersonate",
                        version = "1.0"
                    }
                }
            }, timeout.Token);
            _ = McpJson.Result(await ReadResponseAsync(1, timeout.Token), 1);
            await WriteAsync(new
            {
                jsonrpc = "2.0",
                method = "notifications/initialized",
                @params = new
                {
                }
            }, timeout.Token);
            await WriteAsync(new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new
                {
                    name = tool,
                    arguments
                }
            }, timeout.Token);
            return McpJson.Result(await ReadResponseAsync(2, timeout.Token), 2);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new InvalidOperationException("github_mcp_timeout"); }
        finally { try { if (!process.HasExited) process.Kill(true); } catch { } }

        void Copy(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
                start.Environment[name] = value;
        }
        async Task WriteAsync(object value, CancellationToken token)
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value).AsMemory(), token);
            await process.StandardInput.FlushAsync(token);
        }
        async Task<string> ReadResponseAsync(long id, CancellationToken token)
        {
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(token) ?? throw new InvalidOperationException("github_mcp_malformed_response");
                try
                {
                    using var document = JsonDocument.Parse(line);
                    if (document.RootElement.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.Number && value.GetInt64() == id)
                        return line;
                }
                catch (JsonException) { throw new InvalidOperationException("github_mcp_malformed_response"); }
            }
        }
    }

    private void EnsureAllowed(string tool)
    {
        if (!options.Enabled || !options.Tools.Contains(tool, StringComparer.Ordinal) || options.Tools.Except(["list_pull_requests", "pull_request_read", "create_pull_request", "update_pull_request", "merge_pull_request"], StringComparer.Ordinal).Any())
            throw new InvalidOperationException("github_mcp_tool_not_allowed");
    }
}
