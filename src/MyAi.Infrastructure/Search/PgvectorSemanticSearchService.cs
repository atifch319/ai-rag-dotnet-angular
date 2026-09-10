using Microsoft.EntityFrameworkCore;
using MyAi.Application.Abstractions.Search;
using MyAi.Infrastructure.Persistence;
using Npgsql;
using Pgvector;

namespace MyAi.Infrastructure.Search;

public sealed class PgvectorSemanticSearchService : ISemanticSearchService
{
    private readonly AppDbContext _dbContext;

    public PgvectorSemanticSearchService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SemanticSearchMatch>> SearchByCosineDistanceAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        var queryVector = new Vector(queryEmbedding);
        var vectorParameter = new NpgsqlParameter("queryVector", queryVector)
        {
            DataTypeName = "vector"
        };
        var topKParameter = new NpgsqlParameter("topK", topK);

        // <=> is pgvector cosine distance (1 - cosine similarity). Parameterized; not concatenated SQL.
        var rows = await _dbContext.Database
            .SqlQueryRaw<SemanticSearchRow>(
                """
                SELECT ranked."Id", ranked."DocumentId", ranked."ChunkIndex", ranked."Content", ranked."Distance"
                FROM (
                    SELECT
                        c."Id",
                        c."DocumentId",
                        c."ChunkIndex",
                        c."Content",
                        (c."Embedding" <=> @queryVector) AS "Distance"
                    FROM "DocumentChunks" AS c
                    WHERE c."Embedding" IS NOT NULL
                ) AS ranked
                ORDER BY ranked."Distance"
                LIMIT @topK
                """,
                vectorParameter,
                topKParameter)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new SemanticSearchMatch(
                row.Id,
                row.DocumentId,
                row.ChunkIndex,
                row.Content,
                row.Distance))
            .ToList();
    }

    private sealed class SemanticSearchRow
    {
        public long Id { get; set; }

        public long DocumentId { get; set; }

        public int ChunkIndex { get; set; }

        public string Content { get; set; } = string.Empty;

        public double Distance { get; set; }
    }
}
