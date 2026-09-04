using MediatR;
using Microsoft.EntityFrameworkCore;
using MyAi.Application.Abstractions.Chunking;
using MyAi.Application.Abstractions.Persistence;
using MyAi.Application.Abstractions.TextExtraction;
using MyAi.Application.Common.Exceptions;
using MyAi.Domain.Entities;

namespace MyAi.Application.Features.Documents.ExtractText;

public sealed class ExtractDocumentTextCommandHandler
    : IRequestHandler<ExtractDocumentTextCommand, ExtractDocumentTextResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITextExtractor _textExtractor;
    private readonly IDocumentChunker _documentChunker;
    private readonly IDocumentChunkRepository _documentChunkRepository;

    public ExtractDocumentTextCommandHandler(
        IApplicationDbContext dbContext,
        ITextExtractor textExtractor,
        IDocumentChunker documentChunker,
        IDocumentChunkRepository documentChunkRepository)
    {
        _dbContext = dbContext;
        _textExtractor = textExtractor;
        _documentChunker = documentChunker;
        _documentChunkRepository = documentChunkRepository;
    }

    public async Task<ExtractDocumentTextResponse> Handle(
        ExtractDocumentTextCommand request,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (document is null)
        {
            throw new NotFoundException(nameof(Document), request.Id);
        }

        document.Status = DocumentStatus.Processing;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var extractedText = await _textExtractor.ExtractTextAsync(
                document.FilePath,
                document.ContentType,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new ValidationException("Document", "No readable text was found in the document.");
            }

            var chunkContents = _documentChunker.Chunk(extractedText);
            if (chunkContents.Count == 0)
            {
                throw new ValidationException("Document", "No chunks were generated from the extracted text.");
            }

            var chunks = chunkContents
                .Select((content, index) => new DocumentChunk
                {
                    DocumentId = document.Id,
                    ChunkIndex = index,
                    Content = content
                })
                .ToList();

            await _documentChunkRepository.ReplaceForDocumentAsync(document.Id, chunks, cancellationToken);

            document.ExtractedText = extractedText;
            document.Status = DocumentStatus.Processed;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new ExtractDocumentTextResponse(
                document.Id,
                document.FileName,
                document.Status,
                extractedText.Length,
                chunks.Count);
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
            throw new ValidationException("Document", "Text extraction failed.");
        }
    }
}
