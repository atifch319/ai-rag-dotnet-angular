using Microsoft.EntityFrameworkCore;
using MyAi.Application.Abstractions.Persistence;

namespace MyAi.Infrastructure.Persistence;

/// <summary>
/// Database-first DbContext. Scaffold entities from PostgreSQL into
/// Persistence/Entities and regenerate this context when the schema changes.
/// </summary>
public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
