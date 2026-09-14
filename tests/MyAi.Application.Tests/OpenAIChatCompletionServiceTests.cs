using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Configuration;
using MyAi.Infrastructure.Chat;

namespace MyAi.Application.Tests;

public sealed class OpenAIChatCompletionServiceTests
{
    [Fact]
    public async Task CompleteAsync_MissingApiKey_ThrowsValidationException()
    {
        var service = CreateService(apiKey: string.Empty, chatModel: "gpt-4o-mini");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => service.CompleteAsync("Explain semantic search.", "You are a helpful assistant."));

        Assert.Contains("API key", exception.Errors["OpenAI"][0]);
    }

    [Fact]
    public async Task CompleteAsync_MissingChatModel_ThrowsValidationException()
    {
        var service = CreateService(apiKey: "test-key", chatModel: " ");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => service.CompleteAsync("Explain semantic search.", null));

        Assert.Contains("chat model", exception.Errors["OpenAI"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteAsync_EmptyPrompt_ThrowsValidationException()
    {
        var service = CreateService(apiKey: "test-key", chatModel: "gpt-4o-mini");

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => service.CompleteAsync("   ", "You are a helpful assistant."));

        Assert.Contains("empty", exception.Errors["Prompt"][0], StringComparison.OrdinalIgnoreCase);
    }

    private static OpenAIChatCompletionService CreateService(string apiKey, string chatModel)
    {
        var options = Options.Create(new OpenAIOptions
        {
            ApiKey = apiKey,
            ChatModel = chatModel,
            EmbeddingModel = "text-embedding-3-small",
            EmbeddingDimensions = 1536
        });

        return new OpenAIChatCompletionService(options, NullLogger<OpenAIChatCompletionService>.Instance);
    }
}
