using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Configuration;
using MyAi.Application.Features.Documents.GenerateEmbeddings;
using MyAi.Infrastructure.Embeddings;

namespace MyAi.Application.Tests;

public sealed class GenerateDocumentEmbeddingsCommandValidatorTests
{
    private readonly GenerateDocumentEmbeddingsCommandValidator _validator = new();

    [Fact]
    public void Validate_IdMustBeGreaterThanZero()
    {
        var result = _validator.Validate(new GenerateDocumentEmbeddingsCommand(0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Id");
    }
}

public sealed class OpenAIEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_MissingApiKey_ThrowsValidationException()
    {
        var service = CreateService(apiKey: string.Empty);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => service.GenerateEmbeddingAsync("sample text"));

        Assert.Contains("API key", exception.Errors["OpenAI"][0]);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_EmptyText_ThrowsValidationException()
    {
        var service = CreateService(apiKey: "test-key");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => service.GenerateEmbeddingAsync("   "));

        Assert.Contains("empty", exception.Errors["Text"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_EmptyList_ThrowsValidationException()
    {
        var service = CreateService(apiKey: "test-key");

        await Assert.ThrowsAsync<ValidationException>(
            () => service.GenerateEmbeddingsAsync([]));
    }

    private static OpenAIEmbeddingService CreateService(string apiKey)
    {
        var options = Options.Create(new OpenAIOptions
        {
            ApiKey = apiKey,
            EmbeddingModel = "text-embedding-3-small",
            EmbeddingDimensions = 1536
        });

        return new OpenAIEmbeddingService(options, NullLogger<OpenAIEmbeddingService>.Instance);
    }
}
