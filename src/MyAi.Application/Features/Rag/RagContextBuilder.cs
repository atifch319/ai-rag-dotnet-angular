namespace MyAi.Application.Features.Rag;

public sealed record RagContextChunk(
    string FileName,
    long DocumentId,
    int ChunkIndex,
    string Content);

public static class RagContextBuilder
{
    public static string Build(IReadOnlyList<RagContextChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var sections = chunks.Select(chunk =>
        {
            var fileName = string.IsNullOrWhiteSpace(chunk.FileName) ? "unknown" : chunk.FileName;
            return $"[Source: {fileName} | DocumentId: {chunk.DocumentId} | Chunk: {chunk.ChunkIndex}]{Environment.NewLine}{Environment.NewLine}{chunk.Content}";
        });

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }
}
