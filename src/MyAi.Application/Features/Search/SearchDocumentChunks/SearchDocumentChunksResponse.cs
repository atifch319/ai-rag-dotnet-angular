namespace MyAi.Application.Features.Search.SearchDocumentChunks;

public sealed record SearchDocumentChunksResponse(
    string Query,
    int TopK,
    int ResultCount,
    IReadOnlyList<SemanticSearchResult> Results);

/// <param name="Similarity">
/// Cosine similarity, computed as <c>1 - cosineDistance</c>. Cosine distance is PostgreSQL
/// pgvector's <c>Embedding &lt;=&gt; queryVector</c> operator. Higher values are more similar;
/// identical vectors score 1. This is not a hard-coded or estimated score.
/// </param>
public sealed record SemanticSearchResult(
    long ChunkId,
    long DocumentId,
    int ChunkIndex,
    string Content,
    double Similarity);
