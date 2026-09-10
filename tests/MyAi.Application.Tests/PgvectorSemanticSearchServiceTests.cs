using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyAi.Application.Abstractions.Search;
using MyAi.Domain.Entities;
using MyAi.Infrastructure;
using MyAi.Infrastructure.Persistence;

namespace MyAi.Application.Tests;

public sealed class PgvectorSemanticSearchServiceTests
{
    [Fact]
    public async Task SearchByCosineDistance_UsesPostgresPgvectorAndOrdersByDistance()
    {
        await using var provider = CreateProvider();
        Assert.True(
            provider is not null,
            "PostgreSQL (MyAiDb) must be available for pgvector semantic search tests.");

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var search = scope.ServiceProvider.GetRequiredService<ISemanticSearchService>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var document = new Document
        {
            FileName = $"semantic-search-test-{Guid.NewGuid():N}.txt",
            ContentType = "text/plain",
            FilePath = "uploads/documents/semantic-search-test.txt",
            FileSize = 12,
            Status = DocumentStatus.Embedded,
            UploadedAt = DateTimeOffset.UtcNow
        };
        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync();

        var similar = CreateBasisVector(0);
        var orthogonal = CreateBasisVector(1);
        dbContext.DocumentChunks.AddRange(
            new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = 0,
                Content = "How to request a refund",
                Embedding = similar
            },
            new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = 1,
                Content = "Unrelated shipping information",
                Embedding = orthogonal
            },
            new DocumentChunk
            {
                DocumentId = document.Id,
                ChunkIndex = 2,
                Content = "Chunk without an embedding"
            });
        await dbContext.SaveChangesAsync();

        var matches = await search.SearchByCosineDistanceAsync(similar, topK: 20);

        Assert.NotEmpty(matches);
        Assert.Equal(document.Id, matches[0].DocumentId);
        Assert.Equal("How to request a refund", matches[0].Content);
        Assert.InRange(matches[0].CosineDistance, -0.0001, 0.0001);
        Assert.DoesNotContain(matches, match => match.Content.Contains("without an embedding", StringComparison.Ordinal));

        var inserted = matches.Where(match => match.DocumentId == document.Id).ToList();
        Assert.Equal(2, inserted.Count);
        Assert.True(inserted[0].CosineDistance < inserted[1].CosineDistance);
        Assert.Equal("Unrelated shipping information", inserted[1].Content);

        await transaction.RollbackAsync();
    }

    private static ServiceProvider? CreateProvider()
    {
        var apiPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "MyAi.Api"));

        if (!File.Exists(Path.Combine(apiPath, "appsettings.json")))
        {
            return null;
        }

        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        var secretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            "b8efb54a-3850-4bd5-8f1c-486c73759e61",
            "secrets.json");
        if (File.Exists(secretsPath))
        {
            configurationBuilder.AddJsonFile(secretsPath, optional: false);
        }

        var configuration = configurationBuilder.Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) || !connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            // User-secrets JSON uses "ConnectionStrings:DefaultConnection" as a flat key.
            connectionString = configuration["ConnectionStrings:DefaultConnection"];
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        try
        {
            using var scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!dbContext.Database.CanConnect())
            {
                provider.Dispose();
                return null;
            }

            return provider;
        }
        catch
        {
            provider.Dispose();
            return null;
        }
    }

    private static float[] CreateBasisVector(int index)
    {
        var vector = new float[1536];
        vector[index] = 1f;
        return vector;
    }
}
