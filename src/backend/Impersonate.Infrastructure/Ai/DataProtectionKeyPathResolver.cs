namespace Impersonate.Infrastructure.Ai;

public static class DataProtectionKeyPathResolver
{
    public static string Resolve(string? configured, bool allowDevelopmentDefault)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var expanded = Environment.ExpandEnvironmentVariables(configured.Trim());
            if (!Path.IsPathRooted(expanded))
                throw new InvalidOperationException("Ai:DataProtectionKeyPath must be an absolute path shared by the API and Worker.");
            var configuredPath = Path.GetFullPath(expanded);
            Directory.CreateDirectory(configuredPath);
            return configuredPath;
        }

        if (!allowDevelopmentDefault)
            throw new InvalidOperationException("Ai:DataProtectionKeyPath must be explicitly configured outside Development and Testing.");
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            localData = OperatingSystem.IsWindows() ? Path.Combine(userProfile, "AppData", "Local") : Path.Combine(userProfile, ".local", "share");
        }

        var defaultPath = Path.GetFullPath(Path.Combine(localData, "Impersonate", "data-protection-keys"));
        Directory.CreateDirectory(defaultPath);
        return defaultPath;
    }
}
