using System.Text.Json;
using Impersonate.Application.Delivery;
using Impersonate.Infrastructure.Execution;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Delivery;

internal sealed class ConservativeDeliveryValidationService(DeliveryWorkspaceRegistry workspaces, SafeProcess process, IOptions<Impersonate.Application.Execution.ExecutionOptions> options) : IDeliveryValidationService
{
    public async Task<DeliveryOperationResult<IReadOnlyList<DeliveryValidationStep>>> ValidateAsync(DeliveryWorkspaceReference workspace, CancellationToken ct)
    {
        try
        {
            var root = workspaces.Resolve(workspace);
            var plan = BuildPlan(root);
            if (plan.Count == 0)
                return DeliveryOperationResult<IReadOnlyList<DeliveryValidationStep>>.Fail("delivery_validation_unavailable", "No conservative validation plan is declared by this repository.");
            var results = new List<DeliveryValidationStep>();
            foreach (var step in plan)
            {
                var result = await process.RunAsync(step.Executable, step.Arguments, root, options.Value.CommandTimeoutSeconds, 4000, null, ct);
                if (!result.Succeeded)
                    return DeliveryOperationResult<IReadOnlyList<DeliveryValidationStep>>.Fail("delivery_validation_failed", $"Validation step {step.Name} failed.");
                results.Add(new(step.Name, true, "Completed successfully."));
            }
            return DeliveryOperationResult<IReadOnlyList<DeliveryValidationStep>>.Ok(results);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return DeliveryOperationResult<IReadOnlyList<DeliveryValidationStep>>.Fail("delivery_validation_failed", "Validation could not complete safely."); }
    }

    private static List<Command> BuildPlan(string root)
    {
        var target = FindTarget(root, "*.sln") ?? FindTarget(root, "*.csproj");
        if (target is not null)
        {
            var plan = new List<Command> { new("dotnet-restore", "dotnet", ["restore", target]), new("dotnet-build", "dotnet", ["build", target, "--no-restore"]) };
            if (Directory.EnumerateFiles(root, "*Tests.csproj", SearchOption.AllDirectories).Any())
                plan.Add(new("dotnet-test", "dotnet", ["test", target, "--no-build"]));
            return plan;
        }
        var package = Path.Combine(root, "package.json");
        if (!File.Exists(package) || !File.Exists(Path.Combine(root, "package-lock.json")))
            return [];
        using var document = JsonDocument.Parse(File.ReadAllText(package));
        var scripts = document.RootElement.TryGetProperty("scripts", out var value) ? value : default;
        var npm = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
        var nodePlan = new List<Command> { new("npm-ci", npm, ["ci", "--ignore-scripts"]) };
        foreach (var name in new[] { "lint", "test", "build" })
            if (scripts.ValueKind == JsonValueKind.Object && scripts.TryGetProperty(name, out _))
                nodePlan.Add(new($"npm-{name}", npm, ["run", name]));
        return nodePlan;
    }

    private static string? FindTarget(string root, string pattern) => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
        .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is ".git" or "node_modules" or "bin" or "obj"))
        .Order(StringComparer.Ordinal).FirstOrDefault();

    private sealed record Command(string Name, string Executable, IReadOnlyList<string> Arguments);
}
