using System.Text;

namespace Impersonate.Application.Delivery;

public static class RepositoryPathCanonicalizer
{
    public static string Canonicalize(string value, bool diffSidePrefix = false)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf('\0') >= 0)
            throw new InvalidOperationException("delivery_patch_path_unsafe");
        var path = value[0] == '"' ? DecodeQuoted(value) : value;
        if (diffSidePrefix && (path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal)))
            path = path[2..];
        path = path.Replace('\\', '/');
        if (path.Length == 0 || path.Length > 500 || path.StartsWith("/", StringComparison.Ordinal) || Path.IsPathRooted(path) || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidOperationException("delivery_patch_path_unsafe");
        return path;
    }
    public static IReadOnlyList<string> Set(IEnumerable<string> paths, bool diffSidePrefix = false) => paths.Select(x => Canonicalize(x, diffSidePrefix)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    private static string DecodeQuoted(string value)
    {
        if (value.Length < 2 || value[^1] != '"')
            throw new InvalidOperationException("delivery_patch_path_unsafe");
        var bytes = new List<byte>();
        for (var i = 1; i < value.Length - 1; i++)
        {
            var c = value[i];
            if (c != '\\')
            {
                bytes.AddRange(Encoding.UTF8.GetBytes([c]));
                continue;
            }
            if (++i >= value.Length - 1)
                throw new InvalidOperationException("delivery_patch_path_unsafe");
            c = value[i];
            if (c is '\\' or '"')
                bytes.Add((byte)c);
            else if (c is >= '0' and <= '7')
            {
                var octal = c - '0';
                var count = 1;
                while (count < 3 && i + 1 < value.Length - 1 && value[i + 1] is >= '0' and <= '7')
                {
                    octal = octal * 8 + value[++i] - '0';
                    count++;
                }
                bytes.Add((byte)octal);
            }
            else
                throw new InvalidOperationException("delivery_patch_path_unsafe");
        }
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes.ToArray());
        }
        catch (DecoderFallbackException) { throw new InvalidOperationException("delivery_patch_path_unsafe"); }
    }
}
