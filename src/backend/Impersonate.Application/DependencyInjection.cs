using Microsoft.Extensions.DependencyInjection;
using Impersonate.Application.Projects;

namespace Impersonate.Application;

/// <summary>Registers application-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds the application layer to a service collection.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services.AddScoped<IProjectService, ProjectService>();
}
