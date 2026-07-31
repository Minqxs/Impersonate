using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Impersonate.Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string BackendRoot = Path.Combine(RepositoryRoot, "src", "backend");

    [Fact]
    public void Project_references_follow_clean_architecture_direction()
    {
        AssertReferences(
            typeof(Domain.Pipelines.PipelineRun).Assembly,
            forbidden: ["Impersonate.Application", "Impersonate.Infrastructure", "Impersonate.Api", "Impersonate.Worker"]);
        AssertReferences(
            typeof(Application.Projects.IProjectService).Assembly,
            forbidden: ["Impersonate.Infrastructure", "Impersonate.Api", "Impersonate.Worker"]);
    }

    [Fact]
    public void Domain_and_application_do_not_reference_outer_frameworks()
    {
        var innerAssemblies = new[]
        {
            typeof(Domain.Pipelines.PipelineRun).Assembly,
            typeof(Application.Projects.IProjectService).Assembly
        };

        foreach (var assembly in innerAssemblies)
        {
            AssertReferences(
                assembly,
                forbidden:
                [
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.AspNetCore",
                    "Impersonate.Infrastructure"
                ]);
        }
    }

    [Fact]
    public void Production_files_have_one_matching_top_level_type()
    {
        foreach (var sourceFile in ProductionSourceFiles())
        {
            var root = Parse(sourceFile);
            var topLevelTypes = TopLevelTypes(root).ToArray();
            Assert.True(
                topLevelTypes.Length <= 1,
                $"{Relative(sourceFile)} contains {topLevelTypes.Length} top-level types.");

            if (topLevelTypes.Length == 1)
            {
                Assert.Equal(
                    Path.GetFileNameWithoutExtension(sourceFile),
                    TypeName(topLevelTypes[0]));
            }
        }
    }

    [Fact]
    public void Production_namespaces_match_their_project_layer()
    {
        foreach (var sourceFile in ProductionSourceFiles())
        {
            var relative = Path.GetRelativePath(BackendRoot, sourceFile);
            var project = relative.Split(Path.DirectorySeparatorChar)[0];
            var expectedPrefix = project + ".";
            var root = Parse(sourceFile);

            foreach (var namespaceDeclaration in root.DescendantNodes()
                         .OfType<BaseNamespaceDeclarationSyntax>())
            {
                var namespaceName = namespaceDeclaration.Name.ToString();
                Assert.True(
                    namespaceName.Equals(project, StringComparison.Ordinal)
                    || namespaceName.StartsWith(expectedPrefix, StringComparison.Ordinal),
                    $"{Relative(sourceFile)} declares namespace {namespaceName}, expected {project}.");
            }
        }
    }

    [Fact]
    public void Contract_warehouse_files_are_not_reintroduced()
    {
        var forbiddenExactNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Contracts.cs",
            "Models.cs",
            "Services.cs",
            "Helpers.cs",
            "Common.cs",
            "AiContracts.cs",
            "ExecutionContracts.cs",
            "PipelineContracts.cs",
            "PlannerContracts.cs",
            "ProjectContracts.cs",
            "PipelineModels.cs",
            "RepositoryExecutionServices.cs"
        };

        var violations = ProductionSourceFiles()
            .Where(path => forbiddenExactNames.Contains(Path.GetFileName(path)))
            .Select(Relative)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Presentation_services_do_not_inject_db_context()
    {
        foreach (var project in new[] { "Impersonate.Api", "Impersonate.Worker" })
        {
            foreach (var sourceFile in ProjectSourceFiles(project))
            {
                var constructorParameters = Parse(sourceFile)
                    .DescendantNodes()
                    .OfType<ParameterSyntax>()
                    .Select(parameter => parameter.Type?.ToString())
                    .Where(type => type is not null);

                Assert.DoesNotContain(
                    constructorParameters,
                    type => type!.Contains("DbContext", StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void Infrastructure_implements_core_application_ports()
    {
        var infrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;
        AssertAssignable<Application.Execution.IRepositoryWorkspaceService>(
            RequiredType(infrastructureAssembly, "Impersonate.Infrastructure.Execution.RepositoryWorkspaceService"));
        AssertAssignable<Application.Execution.IRepositoryTools>(
            RequiredType(infrastructureAssembly, "Impersonate.Infrastructure.Execution.SafeRepositoryTools"));
        AssertAssignable<Application.Execution.IExecutionArtifactStore>(
            RequiredType(infrastructureAssembly, "Impersonate.Infrastructure.Execution.LocalExecutionArtifactStore"));
        AssertAssignable<Application.Ai.IProviderCredentialStore>(
            RequiredType(infrastructureAssembly, "Impersonate.Infrastructure.Ai.DataProtectionCredentialStore"));
        AssertAssignable<Application.Pipelines.IPipelineRunRepository>(
            RequiredType(infrastructureAssembly, "Impersonate.Infrastructure.Persistence.EfPipelineRunRepository"));
        AssertAssignable<Application.Delivery.ITaskDeliveryRepository>(
            RequiredType(infrastructureAssembly, "Impersonate.Infrastructure.Persistence.EfTaskDeliveryRepository"));
        AssertAssignable<Application.Delivery.ITargetRepositoryDeliveryService>(
            RequiredType(infrastructureAssembly, "Impersonate.Infrastructure.Delivery.LocalTargetRepositoryDeliveryService"));
        AssertAssignable<Application.Delivery.IDeliveryValidationService>(
            RequiredType(infrastructureAssembly, "Impersonate.Infrastructure.Delivery.ConservativeDeliveryValidationService"));
        AssertAssignable<Application.Delivery.ITaskDeliveryPushService>(
            RequiredType(infrastructureAssembly, "Impersonate.Infrastructure.Delivery.TaskDeliveryPushService"));
        Assert.DoesNotContain(infrastructureAssembly.GetTypes(), type =>
            type != typeof(Application.Delivery.IPullRequestGateway)
            && typeof(Application.Delivery.IPullRequestGateway).IsAssignableFrom(type));
    }

    private static void AssertReferences(Assembly assembly, IReadOnlyCollection<string> forbidden)
    {
        var references = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();

        foreach (var forbiddenReference in forbidden)
        {
            Assert.DoesNotContain(
                references,
                reference => reference.Equals(forbiddenReference, StringComparison.Ordinal)
                    || reference.StartsWith(forbiddenReference + ".", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Delivery_push_never_forces_or_creates_pull_requests()
    {
        var source = File.ReadAllText(Path.Combine(BackendRoot, "Impersonate.Infrastructure", "Delivery", "TaskDeliveryPushService.cs"));
        Assert.DoesNotContain("--force", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IPullRequestGateway", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordPullRequestOpen", source, StringComparison.Ordinal);
    }

    private static void AssertAssignable<TPort>(Type implementation)
    {
        Assert.True(
            typeof(TPort).IsAssignableFrom(implementation),
            $"{implementation.FullName} must implement {typeof(TPort).FullName}.");
    }

    private static Type RequiredType(Assembly assembly, string fullName)
    {
        return assembly.GetType(fullName)
            ?? throw new InvalidOperationException($"{fullName} was not found.");
    }

    private static IEnumerable<string> ProductionSourceFiles()
    {
        return Directory
            .EnumerateFiles(BackendRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasSegment(path, "bin")
                && !HasSegment(path, "obj")
                && !HasSegment(path, "Migrations"));
    }

    private static IEnumerable<string> ProjectSourceFiles(string project)
    {
        return ProductionSourceFiles()
            .Where(path => HasSegment(path, project));
    }

    private static CompilationUnitSyntax Parse(string sourceFile)
    {
        var tree = CSharpSyntaxTree.ParseText(
            File.ReadAllText(sourceFile),
            path: sourceFile);
        var diagnostics = tree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(diagnostics);
        return (CompilationUnitSyntax)tree.GetRoot();
    }

    private static IEnumerable<MemberDeclarationSyntax> TopLevelTypes(
        CompilationUnitSyntax root)
    {
        var members = root.Members;
        var namespaceDeclaration = members
            .OfType<BaseNamespaceDeclarationSyntax>()
            .SingleOrDefault();
        if (namespaceDeclaration is not null)
        {
            members = namespaceDeclaration.Members;
        }

        return members.Where(member =>
            member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);
    }

    private static string TypeName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
            DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
            _ => throw new InvalidOperationException("Unsupported top-level member.")
        };
    }

    private static bool HasSegment(string path, string segment)
    {
        return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);
    }

    private static string Relative(string path)
    {
        return Path.GetRelativePath(RepositoryRoot, path);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "Impersonate.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
