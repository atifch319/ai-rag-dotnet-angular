namespace MyAi.Application.Abstractions.Persistence;

/// <summary>
/// Persistence abstraction used by application handlers.
/// DbSets will be added after EF Core database-first scaffolding.
/// </summary>
public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
