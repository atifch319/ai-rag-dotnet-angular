namespace MyAi.Application.Configuration;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    /// <summary>
    /// Minimum cosine similarity (1 - pgvector cosine distance) required for a
    /// retrieved chunk to be used as RAG context or a citation.
    /// Inclusive: a chunk is kept when similarity &gt;= MinimumSimilarity.
    /// This is a tunable starting point, not a universal relevance score.
    /// Must be between 0 and 1.
    /// </summary>
    public double MinimumSimilarity { get; set; } = 0.25;
}
