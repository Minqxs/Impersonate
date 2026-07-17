using Microsoft.Extensions.DependencyInjection;

namespace Impersonate.Application;

/// <summary>Registers application-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds the application layer to a service collection.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services;
}
