using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyAi.Application.Abstractions.Embeddings;
using MyAi.Application.Abstractions.Persistence;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Configuration;
using MyAi.Domain.Entities;

namespace MyAi.Application.Features.Documents.GenerateEmbeddings;

public sealed class GenerateDocumentEmbeddingsCommandHandler
    : IRequestHandler<GenerateDocumentEmbeddingsCommand, GenerateDocumentEmbeddingsResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDocumentChunkRepository _documentChunkRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly OpenAIOptions _openAiOptions;

    public GenerateDocumentEmbeddingsCommandHandler(
        IApplicationDbContext dbContext,
        IDocumentChunkRepository documentChunkRepository,
        IEmbeddingService embeddingService,
        IOptions<OpenAIOptions> openAiOptions)
    {
        _dbContext = dbContext;
        _documentChunkRepository = documentChunkRepository;
        _embeddingService = embeddingService;
        _openAiOptions = openAiOptions.Value;
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
                var embedding = embeddings[index];
                if (embedding.Length != _openAiOptions.EmbeddingDimensions)
                {
                    throw new ValidationException("Embedding", "Embedding generation failed.");
                }

                chunks[index].Embedding = embedding;
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
        catch (DbUpdateException)
        {
            document.Status = DocumentStatus.Failed;
            throw new ValidationException("Document", "Embedding generation failed.");
        }
        catch (Exception)
        {
            document.Status = DocumentStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new ValidationException("Document", "Embedding generation failed.");
        }
    }
}
