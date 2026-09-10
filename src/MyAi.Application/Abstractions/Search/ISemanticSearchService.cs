namespace MyAi.Application.Abstractions.Search;

/// <summary>
/// PostgreSQL + pgvector cosine-distance search over stored chunk embeddings.
/// </summary>
public interface ISemanticSearchService
{
    /// <summary>
    /// Returns the closest chunks by pgvector cosine distance (<c>Embedding &lt;=&gt; queryVector</c>),
    /// excluding chunks whose embedding is null. Results are ordered by distance ascending
    /// (most similar first). <see cref="SemanticSearchMatch.CosineDistance"/> is the raw
    /// pgvector cosine distance, not cosine similarity.
    /// </summary>
    Task<IReadOnlyList<SemanticSearchMatch>> SearchByCosineDistanceAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default);
}

/// <param name="CosineDistance">
/// pgvector cosine distance from <c>Embedding &lt;=&gt; queryVector</c> (0 = identical direction).
/// Cosine similarity is <c>1 - CosineDistance</c>.
/// </param>
public sealed record SemanticSearchMatch(
    long ChunkId,
    long DocumentId,
    int ChunkIndex,
    string Content,
    double CosineDistance);
