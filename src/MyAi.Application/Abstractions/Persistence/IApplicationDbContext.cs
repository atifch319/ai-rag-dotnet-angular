using Microsoft.EntityFrameworkCore;
using MyAi.Domain.Entities;

namespace MyAi.Application.Abstractions.Persistence;

public interface IApplicationDbContext
{
    DbSet<Document> Documents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
