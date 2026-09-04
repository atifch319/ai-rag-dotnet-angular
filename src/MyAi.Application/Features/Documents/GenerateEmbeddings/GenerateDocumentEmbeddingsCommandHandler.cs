using MediatR;
using Microsoft.EntityFrameworkCore;
using MyAi.Application.Abstractions.Embeddings;
using MyAi.Application.Abstractions.Persistence;
using MyAi.Application.Common.Exceptions;
using MyAi.Domain.Entities;

namespace MyAi.Application.Features.Documents.GenerateEmbeddings;

public sealed class GenerateDocumentEmbeddingsCommandHandler
    : IRequestHandler<GenerateDocumentEmbeddingsCommand, GenerateDocumentEmbeddingsResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDocumentChunkRepository _documentChunkRepository;
    private readonly IEmbeddingService _embeddingService;

    public GenerateDocumentEmbeddingsCommandHandler(
        IApplicationDbContext dbContext,
        IDocumentChunkRepository documentChunkRepository,
        IEmbeddingService embeddingService)
    {
        _dbContext = dbContext;
        _documentChunkRepository = documentChunkRepository;
        _embeddingService = embeddingService;
    }

    public async Task<GenerateDocumentEmbeddingsResponse> Handle(
        GenerateDocumentEmbeddingsCommand request,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (document is null)
        {
            throw new NotFoundException(nameof(Document), request.Id);
        }

        var chunks = await _documentChunkRepository.GetByDocumentIdAsync(document.Id, cancellationToken);
        if (chunks.Count == 0)
        {
            throw new ValidationException(
                "Document",
                "No chunks were found for the document. Extract text before generating embeddings.");
        }

        if (chunks.Any(chunk => string.IsNullOrWhiteSpace(chunk.Content)))
        {
            throw new ValidationException("Document", "One or more chunks have empty content.");
        }

        try
        {
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                chunks.Select(chunk => chunk.Content).ToList(),
                cancellationToken);

            if (embeddings.Count != chunks.Count)
            {
                throw new ValidationException("Document", "Embedding generation failed.");
            }

            for (var index = 0; index < chunks.Count; index++)
            {
                chunks[index].Embedding = embeddings[index];
            }

            document.Status = DocumentStatus.Embedded;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new GenerateDocumentEmbeddingsResponse(
                document.Id,
                chunks.Count,
                chunks.Count,
                "Completed");
        }
        catch (ValidationException)
        {
            document.Status = DocumentStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
        catch (OperationCanceledException)
        {
            document.Status = DocumentStatus.Failed;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception)
        {
            document.Status = DocumentStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new ValidationException("Document", "Embedding generation failed.");
        }
    }
}
