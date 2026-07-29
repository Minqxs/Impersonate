using System.Diagnostics;
using System.Text;
using Impersonate.Application.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Execution;

public sealed class AllowlistedChildProcessEnvironmentBuilder : IChildProcessEnvironmentBuilder
{
    private static readonly string[] WindowsNames = ["SystemRoot", "WINDIR", "PATH", "PATHEXT", "COMSPEC", "USERPROFILE", "HOME", "APPDATA", "LOCALAPPDATA", "TEMP", "TMP", "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432", "DOTNET_ROOT", "NUGET_PACKAGES", "NODE_PATH"];
    private static readonly string[] PortableNames = ["PATH", "HOME", "TEMP", "TMP", "DOTNET_ROOT", "NUGET_PACKAGES", "NODE_PATH"];
    private static readonly string[] NetworkNames = ["HTTP_PROXY", "HTTPS_PROXY", "NO_PROXY", "ALL_PROXY", "SSL_CERT_FILE", "SSL_CERT_DIR", "GIT_SSL_CAINFO"];
    public IReadOnlyDictionary<string, string> Build()
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var parent = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary(x => (string)x.Key, x => (string?)x.Value ?? string.Empty, comparer);
        var result = new Dictionary<string, string>(comparer);
        foreach (var name in (OperatingSystem.IsWindows() ? WindowsNames : PortableNames).Concat(NetworkNames))
            if (parent.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
                result[name] = value;
        return result;
    }
}
