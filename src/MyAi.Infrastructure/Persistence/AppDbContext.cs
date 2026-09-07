using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MyAi.Application.Abstractions.Persistence;
using MyAi.Domain.Entities;
using Pgvector;

namespace MyAi.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        if (Database.IsNpgsql())
        {
            modelBuilder.HasPostgresExtension("vector");

            var embeddingComparer = new ValueComparer<float[]?>(
                (left, right) =>
                    ReferenceEquals(left, right)
                    || (left != null && right != null && left.SequenceEqual(right)),
                value => value == null
                    ? 0
                    : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                value => value == null ? null : value.ToArray());

            modelBuilder.Entity<DocumentChunk>()
                .Property(chunk => chunk.Embedding)
                .HasColumnType(PostgresVectorSetup.EmbeddingColumnType)
                .HasConversion(
                    embedding => embedding == null ? null : new Vector(embedding),
                    vector => vector == null ? null : vector.ToArray(),
                    embeddingComparer);
        }

        base.OnModelCreating(modelBuilder);
    }
}
