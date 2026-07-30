using System.Reflection;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Pipelines;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Agents.Execution;

internal static class PromptLoader
{
    public static string Load(string version)
    {
        if (version is not ("coder-v1" or "reviewer-v1"))
            throw new InvalidOperationException("Unsupported execution prompt version.");
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(x => x.EndsWith($"Prompts.{version}.md", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
