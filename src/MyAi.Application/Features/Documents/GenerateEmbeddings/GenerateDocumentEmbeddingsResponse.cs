namespace MyAi.Application.Features.Documents.GenerateEmbeddings;

public sealed record GenerateDocumentEmbeddingsResponse(
    long DocumentId,
    int ChunkCount,
    int ProcessedCount,
    string Status);
