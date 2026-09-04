using MediatR;
using MyAi.Application.Abstractions.Persistence;
using MyAi.Application.Abstractions.Storage;
using MyAi.Domain.Entities;

namespace MyAi.Application.Features.Documents.Upload;

public sealed class UploadDocumentCommandHandler
    : IRequestHandler<UploadDocumentCommand, DocumentUploadResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDocumentStorageService _storageService;

    public UploadDocumentCommandHandler(
        IApplicationDbContext dbContext,
        IDocumentStorageService storageService)
    {
        _dbContext = dbContext;
        _storageService = storageService;
    }

    public async Task<DocumentUploadResponse> Handle(
        UploadDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var file = request.File;

        var storedPath = await _storageService.StoreAsync(file, cancellationToken);

        var document = new Document
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            FilePath = storedPath,
            UploadedAt = DateTimeOffset.UtcNow,
            Status = DocumentStatus.Uploaded
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DocumentUploadResponse(
            document.Id,
            document.FileName,
            document.ContentType,
            document.FileSize,
            document.Status,
            document.UploadedAt);
    }
}
