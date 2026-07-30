using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public static class RepositoryEvidencePathPolicy
{
    private static readonly string[] SensitiveNames = [".env", ".git", "id_rsa", "id_ed25519", "credentials", "secrets.json"];
    public static bool IsSafe(string path)
    {
        var normalized = PlannerPlanValidator.Normalize(path);
        return !Path.IsPathRooted(path) && normalized != ".." && !normalized.Contains("../", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(normalized) && !normalized.Split('/').Any(part => SensitiveNames.Any(name => part.Equals(name, StringComparison.OrdinalIgnoreCase) || part.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase)));
    }

    public static IReadOnlyList<string> Rank(IEnumerable<string> paths, string featureRequest, int maximum = 500)
    {
        var terms = featureRequest.Split([' ', '-', '_', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(x => x.Length >= 4).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return paths.Select(PlannerPlanValidator.Normalize).Where(IsSafe).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => terms.Any(term => path.Contains(term, StringComparison.OrdinalIgnoreCase)) ? 0 : IsManifest(path) ? 1 : IsArchitecturePath(path) ? 2 : 3).ThenBy(path => path, StringComparer.Ordinal).Take(maximum).ToList();
    }

    private static bool IsManifest(string path) => new[]
    {
        ".sln",
        ".slnx",
        ".csproj",
        "package.json",
        "vite.config",
        "tsconfig",
        "pom.xml",
        "build.gradle",
        "Cargo.toml",
        "go.mod",
        "requirements.txt",
        "pyproject.toml"
    }.Any(name => path.EndsWith(name, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase));
    private static bool IsArchitecturePath(string path) => new[]
    {
        "domain",
        "application",
        "api",
        "frontend",
        "test",
        "src"
    }.Any(part => path.Split('/').Contains(part, StringComparer.OrdinalIgnoreCase));
}
