using MediatR;
using Microsoft.EntityFrameworkCore;
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

    public ExtractDocumentTextCommandHandler(
        IApplicationDbContext dbContext,
        ITextExtractor textExtractor)
    {
        _dbContext = dbContext;
        _textExtractor = textExtractor;
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

            document.ExtractedText = extractedText;
            document.Status = DocumentStatus.Processed;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new ExtractDocumentTextResponse(
                document.Id,
                document.FileName,
                document.Status,
                extractedText.Length);
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
