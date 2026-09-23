using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyAi.Application.Abstractions.Chat;
using MyAi.Application.Abstractions.Embeddings;
using MyAi.Application.Abstractions.Search;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Configuration;
using MyAi.Application.Features.Rag;
using MyAi.Application.Features.Rag.AskRag;
using MyAi.Domain.Entities;
using MyAi.Infrastructure.Persistence;

namespace MyAi.Application.Tests;

public sealed class AskRagQueryHandlerTests
{
    [Fact]
    public async Task Handle_ValidQuery_ReturnsGroundedAnswerAndSources()
    {
        await using var context = CreateContext();
        await SeedDocumentAsync(context);
        var embedding = new RecordingEmbeddingService(CreateVector(1f, 0f));
        var search = new RecordingSearchService(
        [
            new SemanticSearchMatch(15, 5, 3, "Refunds are processed within 7 business days after approval.", 0.18),
            new SemanticSearchMatch(16, 5, 4, "Contact support if the refund is delayed.", 0.35)
        ]);
        var chat = new RecordingChatCompletionService("Your refund should be processed within 7 business days after approval.");
        var handler = CreateHandler(context, embedding, search, chat);

        var response = await handler.Handle(
            new AskRagQuery("When will my refund be processed?", 5),
            CancellationToken.None);

        Assert.Equal("When will my refund be processed?", embedding.LastText);
        Assert.Equal(1, embedding.CallCount);
        Assert.NotNull(search.LastQueryEmbedding);
        Assert.Equal(embedding.LastVector, search.LastQueryEmbedding);
        Assert.Equal(5, search.LastTopK);
        Assert.Equal(1, chat.CallCount);
        Assert.Contains("Refunds are processed within 7 business days after approval.", chat.LastUserPrompt);
        Assert.Contains("[Source: refund-policy.pdf | DocumentId: 5 | Chunk: 3]", chat.LastUserPrompt);
        Assert.Contains("When will my refund be processed?", chat.LastUserPrompt);
        Assert.Contains("only", chat.LastSystemInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("untrusted", chat.LastSystemInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("embedding", chat.LastUserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Your refund should be processed within 7 business days after approval.", response.Answer);
        Assert.Equal(2, response.Sources.Count);
        Assert.Equal(15, response.Sources[0].ChunkId);
        Assert.Equal(5, response.Sources[0].DocumentId);
        Assert.Equal(3, response.Sources[0].ChunkIndex);
        Assert.Equal("refund-policy.pdf", response.Sources[0].FileName);
        Assert.Equal(0.82, response.Sources[0].Similarity, 2);
        Assert.Equal("Refunds are processed within 7 business days after approval.", response.Sources[0].Content);
        Assert.Equal(16, response.Sources[1].ChunkId);
        Assert.Equal(4, response.Sources[1].ChunkIndex);
        Assert.Equal("Contact support if the refund is delayed.", response.Sources[1].Content);
        Assert.True(response.Sources[0].Similarity > response.Sources[1].Similarity);
        Assert.Null(typeof(AskRagResponse).GetProperty("Embedding"));
        Assert.Null(typeof(RagSource).GetProperty("Embedding"));
        Assert.Null(typeof(RagSource).GetProperty("Page"));
        Assert.Null(typeof(RagSource).GetProperty("PageNumber"));
    }

    [Fact]
    public async Task Handle_ValidQuery_PreservesSemanticSearchOrder()
    {
        await using var context = CreateContext();
        await SeedDocumentAsync(context);
        var handler = CreateHandler(
            context,
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            new RecordingSearchService(
            [
                new SemanticSearchMatch(21, 5, 1, "Highest ranked chunk.", 0.12),
                new SemanticSearchMatch(22, 5, 8, "Second ranked chunk.", 0.40)
            ]),
            new RecordingChatCompletionService("Grounded answer."));

        var response = await handler.Handle(new AskRagQuery("What is the refund policy?", 5), CancellationToken.None);

        Assert.Equal(new long[] { 21, 22 }, response.Sources.Select(source => source.ChunkId).ToArray());
        Assert.Equal(0.88, response.Sources[0].Similarity, 2);
        Assert.Equal(0.60, response.Sources[1].Similarity, 2);
    }

    [Fact]
    public async Task Handle_NoResults_DoesNotCallLlm()
    {
        await using var context = CreateContext();
        var chat = new RecordingChatCompletionService("should not be used");
        var handler = CreateHandler(
            context,
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            new RecordingSearchService([]),
            chat);

        var response = await handler.Handle(
            new AskRagQuery("What is the CEO's favorite food?", 5),
            CancellationToken.None);

        Assert.Equal(0, chat.CallCount);
        Assert.Empty(response.Sources);
        Assert.Equal("The answer cannot be determined from the provided documents.", response.Answer);
    }

    [Fact]
    public async Task Handle_UsesConfiguredMinimumSimilarity_IncludesEqualAndAbove_ExcludesBelow()
    {
        await using var context = CreateContext();
        await SeedDocumentAsync(context);
        var chat = new RecordingChatCompletionService("Grounded answer.");
        var handler = CreateHandler(
            context,
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            new RecordingSearchService(
            [
                new SemanticSearchMatch(31, 5, 1, "Above threshold.", 0.20),
                new SemanticSearchMatch(32, 5, 2, "Equal to threshold.", 0.30),
                new SemanticSearchMatch(33, 5, 3, "Below threshold.", 0.40)
            ]),
            chat,
            minimumSimilarity: 0.70);

        var response = await handler.Handle(new AskRagQuery("What is semantic search?", 5), CancellationToken.None);

        Assert.Equal(1, chat.CallCount);
        Assert.Equal(new long[] { 31, 32 }, response.Sources.Select(source => source.ChunkId).ToArray());
        Assert.Equal(0.80, response.Sources[0].Similarity, 2);
        Assert.Equal(0.70, response.Sources[1].Similarity, 2);
        Assert.DoesNotContain(response.Sources, source => source.ChunkId == 33);
        Assert.Null(typeof(AskRagResponse).GetProperty("Embedding"));
    }

    [Fact]
    public async Task Handle_SupportedSemanticSearchQuestion_CallsLlmAndReturnsSources()
    {
        await using var context = CreateContext();
        await SeedDocumentAsync(context, fileName: "RAG_Test_Document_25_Pages.txt");
        var chat = new RecordingChatCompletionService("Semantic search ranks text by meaning.");
        var handler = CreateHandler(
            context,
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            new RecordingSearchService(
            [
                new SemanticSearchMatch(41, 5, 9, "Semantic search focuses on meaning.", 0.592),
                new SemanticSearchMatch(42, 5, 1, "Embeddings represent related ideas nearby.", 0.607),
                new SemanticSearchMatch(43, 5, 4, "Keyword search matches exact terms.", 0.663),
                new SemanticSearchMatch(44, 5, 2, "Retrieval uses vector similarity.", 0.666),
                new SemanticSearchMatch(45, 5, 7, "Relevant chunks ground the answer.", 0.709)
            ]),
            chat);

        var response = await handler.Handle(new AskRagQuery("What is semantic search?", 5), CancellationToken.None);

        Assert.Equal(1, chat.CallCount);
        Assert.Equal(5, response.Sources.Count);
        Assert.Equal(new long[] { 41, 42, 43, 44, 45 }, response.Sources.Select(source => source.ChunkId).ToArray());
        Assert.Equal("Semantic search ranks text by meaning.", response.Answer);
        Assert.All(response.Sources, source => Assert.True(source.Similarity >= 0.25));
    }

    [Fact]
    public async Task Handle_UnsupportedCeoQuestion_SkipsLlmAndHidesWeakSources()
    {
        await using var context = CreateContext();
        await SeedDocumentAsync(context);
        var chat = new RecordingChatCompletionService("should not be used");
        var handler = CreateHandler(
            context,
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            new RecordingSearchService(
            [
                new SemanticSearchMatch(51, 5, 1, "Unrelated policy text.", 0.829),
                new SemanticSearchMatch(52, 5, 2, "Unrelated refund text.", 0.846),
                new SemanticSearchMatch(53, 5, 3, "Unrelated support text.", 0.851),
                new SemanticSearchMatch(54, 5, 4, "Unrelated upload text.", 0.854),
                new SemanticSearchMatch(55, 5, 5, "Unrelated chunking text.", 0.857)
            ]),
            chat);

        var response = await handler.Handle(
            new AskRagQuery("What is the CEO's favorite food?", 5),
            CancellationToken.None);

        Assert.Equal(0, chat.CallCount);
        Assert.Empty(response.Sources);
        Assert.Equal("The answer cannot be determined from the provided documents.", response.Answer);
        Assert.Null(typeof(RagSource).GetProperty("Embedding"));
    }

    [Fact]
    public async Task Constructor_InvalidMinimumSimilarity_Throws()
    {
        await using var context = CreateContext();

        var exception = Assert.Throws<InvalidOperationException>(() => CreateHandler(
            context,
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            new RecordingSearchService([]),
            new RecordingChatCompletionService("unused"),
            minimumSimilarity: 1.5));

        Assert.Contains("MinimumSimilarity", exception.Message);
    }

    [Fact]
    public async Task Handle_LlmFailure_ThrowsValidationException()
    {
        await using var context = CreateContext();
        await SeedDocumentAsync(context);
        var handler = CreateHandler(
            context,
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            new RecordingSearchService(
            [
                new SemanticSearchMatch(15, 5, 3, "Refunds are processed within 7 business days.", 0.1)
            ]),
            new FailingChatCompletionService());

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new AskRagQuery("When will my refund be processed?", 5), CancellationToken.None));

        Assert.Contains("failed", exception.Errors["OpenAI"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_EmbeddingFailure_ThrowsValidationException()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(
            context,
            new FailingEmbeddingService(),
            new RecordingSearchService([]),
            new RecordingChatCompletionService("unused"));

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new AskRagQuery("When will my refund be processed?", 5), CancellationToken.None));

        Assert.Contains("failed", exception.Errors["OpenAI"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_Cancellation_IsPropagated()
    {
        await using var context = CreateContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = CreateHandler(
            context,
            new RecordingEmbeddingService(CreateVector(1f, 0f)),
            new RecordingSearchService([]),
            new RecordingChatCompletionService("unused"));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.Handle(new AskRagQuery("When will my refund be processed?", 5), cts.Token));
    }

    private static AskRagQueryHandler CreateHandler(
        AppDbContext context,
        IEmbeddingService embeddingService,
        ISemanticSearchService searchService,
        IChatCompletionService chatService,
        double minimumSimilarity = 0.25)
    {
        return new AskRagQueryHandler(
            new RagService(
                embeddingService,
                searchService,
                chatService,
                context,
                Options.Create(new OpenAIOptions { EmbeddingDimensions = 1536 }),
                Options.Create(new RagOptions { MinimumSimilarity = minimumSimilarity }),
                NullLogger<RagService>.Instance));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedDocumentAsync(AppDbContext context, string fileName = "refund-policy.pdf")
    {
        context.Documents.Add(new Document
        {
            Id = 5,
            FileName = fileName,
            ContentType = "application/pdf",
            FilePath = "uploads/documents/refund-policy.pdf",
            Status = DocumentStatus.Embedded,
            UploadedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static float[] CreateVector(float first, float second)
    {
        var vector = new float[1536];
        vector[0] = first;
        vector[1] = second;
        return vector;
    }

    private sealed class RecordingEmbeddingService : IEmbeddingService
    {
        public RecordingEmbeddingService(float[] vector)
        {
            LastVector = vector;
        }

        public float[] LastVector { get; }

        public string? LastText { get; private set; }

        public int CallCount { get; private set; }

        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastText = text;
            CallCount++;
            return Task.FromResult(LastVector);
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => LastVector).ToList());
        }
    }

    private sealed class FailingEmbeddingService : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
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

    private sealed class RecordingSearchService : ISemanticSearchService
    {
        private readonly IReadOnlyList<SemanticSearchMatch> _matches;

        public RecordingSearchService(IReadOnlyList<SemanticSearchMatch> matches)
        {
            _matches = matches;
        }

        public float[]? LastQueryEmbedding { get; private set; }

        public int LastTopK { get; private set; }

        public Task<IReadOnlyList<SemanticSearchMatch>> SearchByCosineDistanceAsync(
            float[] queryEmbedding,
            int topK,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastQueryEmbedding = queryEmbedding;
            LastTopK = topK;
            return Task.FromResult(_matches);
        }
    }

    private sealed class RecordingChatCompletionService : IChatCompletionService
    {
        private readonly string _answer;

        public RecordingChatCompletionService(string answer)
        {
            _answer = answer;
        }

        public string? LastUserPrompt { get; private set; }

        public string? LastSystemInstructions { get; private set; }

        public int CallCount { get; private set; }

        public Task<string> CompleteAsync(
            string userPrompt,
            string? systemInstructions,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastUserPrompt = userPrompt;
            LastSystemInstructions = systemInstructions;
            CallCount++;
            return Task.FromResult(_answer);
        }
    }

    private sealed class FailingChatCompletionService : IChatCompletionService
    {
        public Task<string> CompleteAsync(
            string userPrompt,
            string? systemInstructions,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("OpenAI unavailable.");
        }
    }
}
