using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyAi.Application.Abstractions.Chat;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Configuration;
using MyAi.Application.Features.Llm.TestLlm;
using MyAi.Infrastructure;

namespace MyAi.Application.Tests;

public sealed class TestLlmCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidPrompt_ReturnsGeneratedAnswer()
    {
        var chatService = new FakeChatCompletionService("Semantic search ranks text by meaning.");
        var handler = CreateHandler(chatService);

        var response = await handler.Handle(
            new TestLlmCommand("Explain semantic search in one paragraph."),
            CancellationToken.None);

        Assert.Equal("Explain semantic search in one paragraph.", chatService.LastUserPrompt);
        Assert.False(string.IsNullOrWhiteSpace(chatService.LastSystemInstructions));
        Assert.Equal("Explain semantic search in one paragraph.", response.Prompt);
        Assert.Equal("Semantic search ranks text by meaning.", response.Answer);
        Assert.Equal("gpt-4o-mini", response.Model);
        Assert.Equal(1, chatService.CallCount);
    }

    [Fact]
    public async Task Handle_EmptyAnswer_ThrowsValidationException()
    {
        var handler = CreateHandler(new FakeChatCompletionService("   "));

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new TestLlmCommand("Explain semantic search."), CancellationToken.None));

        Assert.Contains("failed", exception.Errors["OpenAI"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_ServiceFailure_ThrowsValidationException()
    {
        var handler = CreateHandler(new FailingChatCompletionService());

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new TestLlmCommand("Explain semantic search."), CancellationToken.None));

        Assert.Contains("failed", exception.Errors["OpenAI"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_Cancellation_IsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = CreateHandler(new FakeChatCompletionService("unused"));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.Handle(new TestLlmCommand("Explain semantic search."), cts.Token));
    }

    [Fact]
    public void AddInfrastructure_RegistersChatCompletionService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=MyAiDb;Username=postgres",
                ["OpenAI:ApiKey"] = string.Empty,
                ["OpenAI:EmbeddingModel"] = "text-embedding-3-small",
                ["OpenAI:ChatModel"] = "gpt-4o-mini"
            })
            .Build();

        services.AddLogging();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var chatService = provider.GetService<IChatCompletionService>();

        Assert.NotNull(chatService);
        Assert.Equal("OpenAIChatCompletionService", chatService.GetType().Name);
    }

    private static TestLlmCommandHandler CreateHandler(IChatCompletionService chatCompletionService)
    {
        return new TestLlmCommandHandler(
            chatCompletionService,
            Options.Create(new OpenAIOptions { ChatModel = "gpt-4o-mini" }));
    }

    private sealed class FakeChatCompletionService : IChatCompletionService
    {
        private readonly string _answer;

        public FakeChatCompletionService(string answer)
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
