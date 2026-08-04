using Impersonate.Application.Delivery;

namespace Impersonate.Infrastructure.Delivery;

internal static class RepositoryFileSetVerifier
{
    public static void Verify(string stage, IReadOnlyList<string> actual, IReadOnlyList<string> approved)
    {
        var actualSet = RepositoryPathCanonicalizer.Set(actual);
        var approvedSet = RepositoryPathCanonicalizer.Set(approved);
        if (actualSet.Count == 0 || !actualSet.ToHashSet(StringComparer.Ordinal).SetEquals(approvedSet))
            throw Mismatch(stage, approvedSet, actualSet);
    }
    private static InvalidOperationException Mismatch(string stage, IReadOnlyList<string> approved, IReadOnlyList<string> actual)
    {
        var evidence = $"category=delivery_changed_files_mismatch; stage={stage}; approved={approved.Count}; actual={actual.Count}; missing=[{string.Join(',', approved.Except(actual, StringComparer.Ordinal).Take(5))}]; unexpected=[{string.Join(',', actual.Except(approved, StringComparer.Ordinal).Take(5))}]";
        if (evidence.Length > 900)
            evidence = evidence[..900];
        return new($"delivery_{stage}_file_set_mismatch:{evidence}");
    }
}
