using Impersonate.Application.Delivery;

namespace Impersonate.Infrastructure.Delivery;

internal static class GitPatchPathParser
{
    public static IReadOnlyList<string> Parse(string patch)
    {
        var paths = new List<string>();
        foreach (var line in patch.Split('\n'))
        {
            var header = line.TrimEnd('\r');
            if (!header.StartsWith("diff --git ", StringComparison.Ordinal))
                continue;
            var pair = ParsePair(header[11..]);
            var left = RepositoryPathCanonicalizer.Canonicalize(pair.Left, true);
            var right = RepositoryPathCanonicalizer.Canonicalize(pair.Right, true);
            if (!string.Equals(left, right, StringComparison.Ordinal))
                throw new InvalidOperationException("delivery_patch_rename_unsupported");
            paths.Add(right);
        }
        return RepositoryPathCanonicalizer.Set(paths);
    }

    private static (string Left, string Right) ParsePair(string value)
    {
        if (value.StartsWith('"'))
        {
            var end = QuotedEnd(value);
            if (end < 0 || end + 2 >= value.Length || value[end + 1] != ' ')
                throw new InvalidOperationException("delivery_patch_path_unsafe");
            var right = value[(end + 2)..];
            if (!right.StartsWith('"') || QuotedEnd(right) != right.Length - 1)
                throw new InvalidOperationException("delivery_patch_path_unsafe");
            return (value[..(end + 1)], right);
        }
        var matches = new List<(string Left, string Right)>();
        for (var split = value.IndexOf(" b/", StringComparison.Ordinal); split > 0; split = value.IndexOf(" b/", split + 1, StringComparison.Ordinal))
        {
            var left = value[..split];
            var right = value[(split + 1)..];
            if (left.StartsWith("a/", StringComparison.Ordinal) && right.StartsWith("b/", StringComparison.Ordinal) && string.Equals(left[2..], right[2..], StringComparison.Ordinal))
                matches.Add((left, right));
        }
        return matches.Count == 1 ? matches[0] : throw new InvalidOperationException("delivery_patch_path_unsafe");
    }

    private static int QuotedEnd(string value)
    {
        var escaped = false;
        for (var i = 1; i < value.Length; i++)
        {
            if (!escaped && value[i] == '"')
                return i;
            if (!escaped && value[i] == '\\')
                escaped = true;
            else
                escaped = false;
        }
        return -1;
    }
}
