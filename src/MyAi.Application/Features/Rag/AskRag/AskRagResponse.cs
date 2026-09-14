namespace MyAi.Application.Features.Rag.AskRag;

public sealed record AskRagResponse(
    string Query,
    string Answer,
    IReadOnlyList<RagSource> Sources);

public sealed record RagSource(
    long DocumentId,
    long ChunkId,
    int ChunkIndex,
    string FileName,
    double Similarity);
