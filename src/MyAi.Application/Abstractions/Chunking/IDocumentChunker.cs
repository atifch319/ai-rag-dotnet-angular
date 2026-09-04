namespace MyAi.Application.Abstractions.Chunking;

public interface IDocumentChunker
{
    IReadOnlyList<string> Chunk(string text);
}
