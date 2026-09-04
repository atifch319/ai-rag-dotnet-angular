namespace MyAi.Application.Features.Documents.ExtractText;

public sealed record ExtractDocumentTextResponse(
    long Id,
    string FileName,
    string Status,
    int TextLength,
    int ChunkCount);
