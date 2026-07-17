using Microsoft.EntityFrameworkCore;

namespace Impersonate.Infrastructure.Persistence;

/// <summary>Database context for persistence introduced by future application modules.</summary>
public sealed class ImpersonateDbContext(DbContextOptions<ImpersonateDbContext> options) : DbContext(options);
