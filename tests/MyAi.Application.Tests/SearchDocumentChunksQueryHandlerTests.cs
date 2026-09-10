using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyAi.Application.Abstractions.Embeddings;
using MyAi.Application.Abstractions.Search;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Configuration;
using MyAi.Application.Features.Search.SearchDocumentChunks;
using MyAi.Infrastructure;

namespace MyAi.Application.Tests;

public sealed class SearchDocumentChunksQueryHandlerTests
{
    [Fact]
    public async Task Handle_ValidRequest_ReturnsChunksOrderedBySimilarity()
    {
        var queryVector = CreateVector(1f, 0f);
        var embeddingService = new RecordingEmbeddingService(queryVector);
        var searchService = new CosineRankingSearchService(
        [
            new StoredChunk(10, 1, 0, "Refund policy for orders", CreateVector(0.2f, 0.8f)),
            new StoredChunk(11, 1, 1, "How to request a refund", CreateVector(1f, 0f)),
            new StoredChunk(12, 2, 0, "Shipping delays", CreateVector(0f, 1f))
        ]);

        var handler = CreateHandler(embeddingService, searchService);
        var response = await handler.Handle(
            new SearchDocumentChunksQuery("How can I get a refund?", 2),
            CancellationToken.None);

        Assert.Equal("How can I get a refund?", embeddingService.LastText);
        Assert.Equal("How can I get a refund?", response.Query);
        Assert.Equal(2, response.TopK);
        Assert.Equal(2, response.ResultCount);
        Assert.Equal(2, response.Results.Count);
        Assert.Equal(11, response.Results[0].ChunkId);
        Assert.Equal("How to request a refund", response.Results[0].Content);
        Assert.True(response.Results[0].Similarity > response.Results[1].Similarity);
        Assert.All(response.Results, result => Assert.InRange(result.Similarity, -1d, 1d));
    }

    [Fact]
    public async Task Handle_GeneratesQueryEmbeddingViaExistingService()
    {
        var embeddingService = new RecordingEmbeddingService(CreateVector(1f, 0f));
        var handler = CreateHandler(embeddingService, new CosineRankingSearchService([]));

        await handler.Handle(new SearchDocumentChunksQuery("refund policy", 5), CancellationToken.None);

        Assert.Equal("refund policy", embeddingService.LastText);
        Assert.Equal(1, embeddingService.CallCount);
    }

    [Fact]
    public async Task Handle_NoMatchingChunks_ReturnsEmptyResults()
    {
        var handler = CreateHandler(
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            new CosineRankingSearchService([]));

        var response = await handler.Handle(
            new SearchDocumentChunksQuery("anything", 5),
            CancellationToken.None);

        Assert.Equal(0, response.ResultCount);
        Assert.Empty(response.Results);
    }

    [Fact]
    public async Task Handle_ChunksWithoutEmbeddings_AreExcluded()
    {
        var searchService = new CosineRankingSearchService(
        [
            new StoredChunk(1, 1, 0, "Has embedding", CreateVector(1f, 0f)),
            new StoredChunk(2, 1, 1, "Missing embedding", Embedding: null)
        ]);
        var handler = CreateHandler(
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            searchService);

        var response = await handler.Handle(
            new SearchDocumentChunksQuery("Has embedding", 5),
            CancellationToken.None);

        var result = Assert.Single(response.Results);
        Assert.Equal(1, result.ChunkId);
        Assert.DoesNotContain(response.Results, item => item.ChunkId == 2);
    }

    [Fact]
    public async Task Handle_DoesNotExposeEmbeddingVectors()
    {
        var handler = CreateHandler(
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            new CosineRankingSearchService(
            [
                new StoredChunk(1, 1, 0, "Refund help", CreateVector(1f, 0f))
            ]));

        var response = await handler.Handle(
            new SearchDocumentChunksQuery("refund", 5),
            CancellationToken.None);

        Assert.Null(typeof(SemanticSearchResult).GetProperty("Embedding"));
        Assert.Null(typeof(SearchDocumentChunksResponse).GetProperty("Embedding"));

        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("embedding", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("similarity", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chunkId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_OpenAIFailure_ThrowsValidationException()
    {
        var handler = CreateHandler(
            new FailingEmbeddingService(),
            new CosineRankingSearchService([]));

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new SearchDocumentChunksQuery("refund", 5), CancellationToken.None));

        Assert.Contains("failed", exception.Errors["OpenAI"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddInfrastructure_RegistersSemanticSearchService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=MyAiDb;Username=postgres",
                ["OpenAI:ApiKey"] = string.Empty,
                ["OpenAI:EmbeddingModel"] = "text-embedding-3-small"
            })
            .Build();

        services.AddLogging();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var searchService = provider.GetService<ISemanticSearchService>();

        Assert.NotNull(searchService);
        Assert.Equal("PgvectorSemanticSearchService", searchService.GetType().Name);
    }

    private static SearchDocumentChunksQueryHandler CreateHandler(
        IEmbeddingService embeddingService,
        ISemanticSearchService semanticSearchService)
    {
        return new SearchDocumentChunksQueryHandler(
            embeddingService,
            semanticSearchService,
            Options.Create(new OpenAIOptions { EmbeddingDimensions = 1536 }));
    }

    private static float[] CreateVector(float first, float second)
    {
        var vector = new float[1536];
        vector[0] = first;
        vector[1] = second;
        return vector;
    }

    private sealed record StoredChunk(
        long Id,
        long DocumentId,
        int ChunkIndex,
        string Content,
        float[]? Embedding);

    /// <summary>
    /// Ranks seeded vectors by real cosine distance. Does not return a hard-coded result list.
    /// </summary>
    private sealed class CosineRankingSearchService : ISemanticSearchService
    {
        private readonly IReadOnlyList<StoredChunk> _chunks;

        public CosineRankingSearchService(IReadOnlyList<StoredChunk> chunks)
        {
            _chunks = chunks;
        }

        public Task<IReadOnlyList<SemanticSearchMatch>> SearchByCosineDistanceAsync(
            float[] queryEmbedding,
            int topK,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<SemanticSearchMatch> matches = _chunks
                .Where(chunk => chunk.Embedding is not null)
                .Select(chunk => new SemanticSearchMatch(
                    chunk.Id,
                    chunk.DocumentId,
                    chunk.ChunkIndex,
                    chunk.Content,
                    CosineDistance(queryEmbedding, chunk.Embedding!)))
                .OrderBy(match => match.CosineDistance)
                .Take(topK)
                .ToList();

            return Task.FromResult(matches);
        }

        private static double CosineDistance(float[] left, float[] right)
        {
            double dot = 0;
            double leftNorm = 0;
            double rightNorm = 0;
            for (var index = 0; index < left.Length; index++)
            {
                dot += left[index] * right[index];
                leftNorm += left[index] * left[index];
                rightNorm += right[index] * right[index];
            }

            var similarity = dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
            return 1d - similarity;
        }
    }

    private sealed class RecordingEmbeddingService : IEmbeddingService
    {
        private readonly float[] _vector;

        public RecordingEmbeddingService(float[] vector)
        {
            _vector = vector;
        }

        public string? LastText { get; private set; }

        public int CallCount { get; private set; }

        public Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            LastText = text;
            CallCount++;
            return Task.FromResult(_vector);
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => _vector).ToList());
        }
    }

    private sealed class FailingEmbeddingService : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("OpenAI unavailable.");
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("OpenAI unavailable.");
        }
    }
}
