using Microsoft.Extensions.Options;
using MyAi.Application.Chunking;
using MyAi.Application.Configuration;

namespace MyAi.Application.Tests;

public sealed class DocumentChunkerTests
{
    [Fact]
    public void Chunk_EmptyText_ReturnsNoChunks()
    {
        var chunker = CreateChunker();

        Assert.Empty(chunker.Chunk(string.Empty));
        Assert.Empty(chunker.Chunk("   \n\t  "));
    }

    [Fact]
    public void Chunk_SmallDocument_ReturnsSingleChunk()
    {
        var chunker = CreateChunker(chunkSize: 1000, overlap: 150);
        var text = "This is a small document with only a few words.";

        var chunks = chunker.Chunk(text);

        var chunk = Assert.Single(chunks);
        Assert.Equal(text, chunk);
    }

    [Fact]
    public void Chunk_LargeDocument_SplitsIntoMultipleChunks()
    {
        var chunker = CreateChunker(chunkSize: 10, overlap: 2);
        var text = string.Join(' ', Enumerable.Range(1, 35).Select(i => $"word{i}"));

        var chunks = chunker.Chunk(text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk)));
    }

    [Fact]
    public void Chunk_AssignsSequentialContentMatchingChunkIndexOrder()
    {
        var chunker = CreateChunker(chunkSize: 5, overlap: 1);
        var text = string.Join(' ', Enumerable.Range(1, 18).Select(i => $"word{i}"));

        var chunks = chunker.Chunk(text);

        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select((_, index) => index));
        Assert.StartsWith("word1", chunks[0]);
    }

    [Fact]
    public void Chunk_AppliesOverlapBetweenConsecutiveChunks()
    {
        var chunker = CreateChunker(chunkSize: 4, overlap: 2);
        var text = "alpha bravo charlie delta echo foxtrot golf hotel";

        var chunks = chunker.Chunk(text);

        Assert.True(chunks.Count >= 2);

        var firstWords = chunks[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var secondWords = chunks[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = firstWords.TakeLast(2).ToArray();

        Assert.Equal(overlap[0], secondWords[0]);
        Assert.Equal(overlap[1], secondWords[1]);
    }

    [Fact]
    public void Chunk_DoesNotCreateEmptyChunks()
    {
        var chunker = CreateChunker(chunkSize: 6, overlap: 2);
        var text = "  one   two.\n\nthree four five   six seven eight nine  ";

        var chunks = chunker.Chunk(text);

        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk =>
        {
            Assert.False(string.IsNullOrWhiteSpace(chunk));
            Assert.Equal(chunk.Trim(), chunk);
        });
    }

    [Fact]
    public void Chunk_DuplicateExecution_IsDeterministic()
    {
        var chunker = CreateChunker(chunkSize: 8, overlap: 3);
        var text = string.Join(' ', Enumerable.Range(1, 40).Select(i => $"token{i}"));

        var first = chunker.Chunk(text);
        var second = chunker.Chunk(text);

        Assert.Equal(first, second);
    }

    private static DocumentChunker CreateChunker(int chunkSize = 1000, int overlap = 150)
    {
        var options = Options.Create(new DocumentChunkingOptions
        {
            ChunkSize = chunkSize,
            ChunkOverlap = overlap
        });

        return new DocumentChunker(options);
    }
}
