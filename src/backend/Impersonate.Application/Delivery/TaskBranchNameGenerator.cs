using System.Text;

namespace Impersonate.Application.Delivery;

public static class TaskBranchNameGenerator
{
    public static string Create(Guid runId, int sequence, string title, string patchSha)
    {
        var slug = new StringBuilder();
        foreach (var ch in title.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
                slug.Append(ch);
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
            if (slug.Length == 40)
                break;
        }
        var value = slug.ToString().Trim('-');
        if (value.Length == 0)
            value = "task";
        return $"impersonate/{runId:N}"[..20] + $"/{sequence:D3}-{value}-{patchSha[..8].ToLowerInvariant()}";
    }
}
