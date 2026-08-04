using System.Text.Json;

namespace Impersonate.Infrastructure.Delivery.Mcp;

internal interface IGitHubMcpClient
{
    string ServerIdentity
    {
        get;
    }
    Task<JsonElement> CallToolAsync(string tool, object arguments, CancellationToken cancellationToken);
}
