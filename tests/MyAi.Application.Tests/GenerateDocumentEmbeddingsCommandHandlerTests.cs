using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyAi.Application.Abstractions.Embeddings;
using MyAi.Application.Abstractions.Persistence;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Features.Documents.GenerateEmbeddings;
using MyAi.Domain.Entities;
using MyAi.Infrastructure;
using MyAi.Infrastructure.Persistence;
using MyAi.Infrastructure.Persistence.Repositories;

namespace MyAi.Application.Tests;

public sealed class GenerateDocumentEmbeddingsCommandHandlerTests
{
    [Fact]
    public async Task Handle_DocumentNotFound_ThrowsNotFoundException()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(context, new FakeEmbeddingService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GenerateDocumentEmbeddingsCommand(99), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DocumentWithoutChunks_ThrowsValidationException()
    {
        await using var context = CreateContext();
        context.Documents.Add(new Document
        {
            Id = 1,
            FileName = "doc.txt",
            ContentType = "text/plain",
            FilePath = "uploads/documents/doc.txt",
            Status = DocumentStatus.Processed,
            UploadedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, new FakeEmbeddingService());

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new GenerateDocumentEmbeddingsCommand(1), CancellationToken.None));

        Assert.Contains("No chunks", exception.Errors["Document"][0]);
    }

    [Fact]
    public async Task Handle_SuccessfulGeneration_PersistsEmbeddings()
    {
        await using var context = CreateContext();
        var document = new Document
        {
            Id = 1,
            FileName = "doc.txt",
            ContentType = "text/plain",
            FilePath = "uploads/documents/doc.txt",
            Status = DocumentStatus.Processed,
            UploadedAt = DateTimeOffset.UtcNow
        };
        context.Documents.Add(document);
        context.DocumentChunks.Add(new DocumentChunk
        {
            Id = 1,
            DocumentId = 1,
            ChunkIndex = 0,
            Content = "First chunk"
        });
        context.DocumentChunks.Add(new DocumentChunk
        {
            Id = 2,
            DocumentId = 1,
            ChunkIndex = 1,
            Content = "Second chunk"
        });
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, new FakeEmbeddingService());
        var response = await handler.Handle(new GenerateDocumentEmbeddingsCommand(1), CancellationToken.None);

        Assert.Equal("Completed", response.Status);
        Assert.Equal(2, response.ChunkCount);
        Assert.Equal(2, response.ProcessedCount);

        var chunks = await context.DocumentChunks.OrderBy(chunk => chunk.ChunkIndex).ToListAsync();
        Assert.All(chunks, chunk =>
        {
            Assert.NotNull(chunk.Embedding);
            Assert.Equal(1536, chunk.Embedding.Length);
        });
        Assert.Equal(DocumentStatus.Embedded, document.Status);
    }

    [Fact]
    public void AddInfrastructure_RegistersEmbeddingService()
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
        var embeddingService = provider.GetService<IEmbeddingService>();

        Assert.NotNull(embeddingService);
    }

    private static GenerateDocumentEmbeddingsCommandHandler CreateHandler(
        AppDbContext context,
        IEmbeddingService embeddingService)
    {
        return new GenerateDocumentEmbeddingsCommandHandler(
            context,
            new DocumentChunkRepository(context),
            embeddingService);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeEmbeddingService : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ValidationException("Text", "Chunk content must not be empty.");
            }

            return Task.FromResult(CreateVector());
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<float[]> embeddings = texts.Select(_ => CreateVector()).ToList();
            return Task.FromResult(embeddings);
        }

        private static float[] CreateVector()
        {
            return Enumerable.Repeat(0.1f, 1536).ToArray();
        }
    }
}
