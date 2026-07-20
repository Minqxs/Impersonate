using Impersonate.Infrastructure.Persistence;
using Impersonate.Application.Projects;
using Impersonate.Application.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Impersonate.Infrastructure;

/// <summary>Registers infrastructure-layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds configured infrastructure services without forcing database access at startup.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ImpersonateDatabase");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ImpersonateDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<IProjectRepository, EfProjectRepository>();
            services.AddScoped<IPipelineRunRepository, EfPipelineRunRepository>();
        }

        return services;
    }
}
