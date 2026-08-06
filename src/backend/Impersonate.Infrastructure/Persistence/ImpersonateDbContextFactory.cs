using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Impersonate.Infrastructure.Persistence;

public sealed class ImpersonateDbContextFactory : IDesignTimeDbContextFactory<ImpersonateDbContext>
{
    public ImpersonateDbContext CreateDbContext(string[] args)
    {
        var connectionString = TryGetConnectionStringArgument(args) ?? BuildConfiguration().GetConnectionString("ImpersonateDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("A design-time database connection string is required. Set ConnectionStrings__ImpersonateDatabase or configure ConnectionStrings:ImpersonateDatabase in the API appsettings files.");

        var options = new DbContextOptionsBuilder<ImpersonateDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ImpersonateDbContext(options);
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var root = FindRepositoryRoot();
        var apiConfigurationPath = Path.Combine(root, "src", "backend", "Impersonate.Api");

        return new ConfigurationBuilder()
            .SetBasePath(apiConfigurationPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Impersonate.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? TryGetConnectionStringArgument(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--connection", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];

            const string prefix = "--connection=";
            if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return args[i][prefix.Length..];
        }

        return null;
    }
}
