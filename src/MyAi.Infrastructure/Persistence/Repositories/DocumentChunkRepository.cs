using Microsoft.EntityFrameworkCore;
using MyAi.Application.Abstractions.Persistence;
using MyAi.Domain.Entities;
using MyAi.Infrastructure.Persistence;

namespace MyAi.Infrastructure.Persistence.Repositories;

public sealed class DocumentChunkRepository : IDocumentChunkRepository
{
    private readonly AppDbContext _dbContext;

    public DocumentChunkRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ReplaceForDocumentAsync(
        long documentId,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        var existingChunks = await _dbContext.DocumentChunks
            .Where(chunk => chunk.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        if (existingChunks.Count > 0)
        {
            _dbContext.DocumentChunks.RemoveRange(existingChunks);
        }

        if (chunks.Count > 0)
        {
            await _dbContext.DocumentChunks.AddRangeAsync(chunks, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DocumentChunks
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToListAsync(cancellationToken);
    }
}
