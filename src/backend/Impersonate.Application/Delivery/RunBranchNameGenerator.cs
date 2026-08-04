using System.Text;

namespace Impersonate.Application.Delivery;

public static class RunBranchNameGenerator
{
    public static string Create(Guid runId, string featureRequest)
    {
        var slug = new StringBuilder();
        foreach (var ch in featureRequest.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
                slug.Append(ch);
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
            if (slug.Length == 48)
                break;
        }
        var value = slug.ToString().Trim('-');
        if (value.Length == 0)
            value = "feature";
        return $"impersonate/run-{runId:N}"[..23] + $"-{value}";
    }
}
