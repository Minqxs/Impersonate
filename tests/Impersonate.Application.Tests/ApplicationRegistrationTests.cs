using Impersonate.Application;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Impersonate.Application.Tests;

public sealed class ApplicationRegistrationTests
{
    [Fact]
    public void AddApplication_CreatesAValidServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider);
    }
}
