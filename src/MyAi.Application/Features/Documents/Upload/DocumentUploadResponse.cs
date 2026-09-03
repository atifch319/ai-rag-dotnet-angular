namespace MyAi.Application.Features.Documents.Upload;

public sealed record DocumentUploadResponse(
    long Id,
    string FileName,
    string ContentType,
    long FileSize,
    string Status,
    DateTimeOffset UploadedAt);
