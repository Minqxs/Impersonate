using System.Text.Json;

namespace Impersonate.Infrastructure.Delivery.Mcp;

internal static class McpJson
{
    public static JsonElement Result(string payload, long id)
    {
        var json = payload.TrimStart().StartsWith("data:", StringComparison.Ordinal) ? string.Join("", payload.Split('\n').Where(x => x.StartsWith("data:", StringComparison.Ordinal)).Select(x => x[5..].Trim())) : payload;
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("error", out _))
            throw new InvalidOperationException("github_mcp_error");
        if (!root.TryGetProperty("id", out var responseId) || responseId.GetInt64() != id || !root.TryGetProperty("result", out var result))
            throw new InvalidOperationException("github_mcp_malformed_response");
        if (result.TryGetProperty("isError", out var isError) && isError.ValueKind == JsonValueKind.True)
            throw new InvalidOperationException("github_mcp_tool_error");
        if (result.TryGetProperty("structuredContent", out var structured))
            return structured.Clone();
        if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            foreach (var item in content.EnumerateArray())
                if (item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    try
                    {
                        using var nested = JsonDocument.Parse(text.GetString()!);
                        return nested.RootElement.Clone();
                    }
                    catch (JsonException) { }
                }
        return result.Clone();
    }
}
