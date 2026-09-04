namespace MyAi.Application.Configuration;

public sealed class DocumentChunkingOptions
{
    public const string SectionName = "DocumentChunking";

    public int ChunkSize { get; set; } = 1000;

    public int ChunkOverlap { get; set; } = 150;
}
