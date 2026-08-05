namespace Impersonate.Infrastructure.Delivery.Mcp;

public static class GitHubRepositoryIdentity
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var candidate = value.Trim().TrimEnd('/');
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                return null;
            candidate = uri.AbsolutePath.Trim('/');
        }
        if (candidate.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            candidate = candidate[..^4];
        var parts = candidate.Split('/');
        return parts.Length == 2 && parts.All(IsPart) ? $"{parts[0]}/{parts[1]}" : null;
    }
    private static bool IsPart(string value) => value.Length is > 0 and <= 100 && value.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.');
}
