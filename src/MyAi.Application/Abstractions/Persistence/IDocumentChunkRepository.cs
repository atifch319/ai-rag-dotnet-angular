using MyAi.Domain.Entities;

namespace MyAi.Application.Abstractions.Persistence;

public interface IDocumentChunkRepository
{
    Task ReplaceForDocumentAsync(
        long documentId,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdAsync(
        long documentId,
        CancellationToken cancellationToken = default);
}
