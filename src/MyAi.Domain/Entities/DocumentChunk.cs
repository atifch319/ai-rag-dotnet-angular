namespace MyAi.Domain.Entities;

public class DocumentChunk
{
    public long Id { get; set; }

    public long DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public float[]? Embedding { get; set; }

    public Document Document { get; set; } = null!;
}
