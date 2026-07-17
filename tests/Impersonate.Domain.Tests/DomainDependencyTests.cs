using System.Reflection;
using Impersonate.Domain;

namespace Impersonate.Domain.Tests;

public sealed class DomainDependencyTests
{
    [Fact]
    public void DomainAssembly_DoesNotReferenceTechnicalFrameworks()
    {
        var references = typeof(AssemblyMarker).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        Assert.DoesNotContain(references, name => name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(references, name => name?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true);
    }
}
