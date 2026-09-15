using MyAi.Application.Abstractions.Rag;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Features.Chat.SendChatMessage;
using MyAi.Application.Features.Rag.AskRag;

namespace MyAi.Application.Tests;

public sealed class ChatQueryHandlerTests
{
    [Fact]
    public async Task Handle_ValidMessage_InvokesRagAndReturnsAnswerAndSources()
    {
        var rag = new RecordingRagService(new AskRagResponse(
            "What is semantic search?",
            "Semantic search focuses on meaning rather than exact keyword matches.",
            [
                new RagSource(5, 39, 9, "RAG_Test_Document_25_Pages.txt", 0.4076)
            ]));
        var handler = new ChatQueryHandler(rag);

        var response = await handler.Handle(new ChatQuery("What is semantic search?", 5), CancellationToken.None);

        Assert.Equal("What is semantic search?", rag.LastQuery);
        Assert.Equal(5, rag.LastTopK);
        Assert.Equal(1, rag.CallCount);
        Assert.Equal("What is semantic search?", response.Message);
        Assert.Equal("Semantic search focuses on meaning rather than exact keyword matches.", response.Answer);
        var source = Assert.Single(response.Sources);
        Assert.Equal(5, source.DocumentId);
        Assert.Equal(39, source.ChunkId);
        Assert.Equal(9, source.ChunkIndex);
        Assert.Equal("RAG_Test_Document_25_Pages.txt", source.FileName);
        Assert.Equal(0.4076, source.Similarity);
        Assert.Null(typeof(ChatResponse).GetProperty("Embedding"));
    }

    [Fact]
    public async Task Handle_NoRelevantInformation_PreservesRagResponse()
    {
        var rag = new RecordingRagService(new AskRagResponse(
            "What is the CEO's favorite food?",
            "No relevant information was found in the uploaded documents.",
            []));
        var handler = new ChatQueryHandler(rag);

        var response = await handler.Handle(
            new ChatQuery("What is the CEO's favorite food?", 5),
            CancellationToken.None);

        Assert.Equal(1, rag.CallCount);
        Assert.Empty(response.Sources);
        Assert.Contains("No relevant information", response.Answer);
    }

    [Fact]
    public async Task Handle_LlmFailure_ThrowsValidationException()
    {
        var handler = new ChatQueryHandler(new FailingRagService());

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new ChatQuery("What is semantic search?", 5), CancellationToken.None));

        Assert.Contains("failed", exception.Errors["OpenAI"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_Cancellation_IsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new ChatQueryHandler(new RecordingRagService(
            new AskRagResponse("unused", "unused", [])));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.Handle(new ChatQuery("What is semantic search?", 5), cts.Token));
    }

    private sealed class RecordingRagService : IRagService
    {
        private readonly AskRagResponse _response;

        public RecordingRagService(AskRagResponse response)
        {
            _response = response;
        }

        public string? LastQuery { get; private set; }

        public int LastTopK { get; private set; }

        public int CallCount { get; private set; }

        public Task<AskRagResponse> AskAsync(string query, int topK, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastQuery = query;
            LastTopK = topK;
            CallCount++;
            return Task.FromResult(_response);
        }
    }

    private sealed class FailingRagService : IRagService
    {
        public Task<AskRagResponse> AskAsync(string query, int topK, CancellationToken cancellationToken = default)
        {
            throw new ValidationException("OpenAI", "Chat completion failed.");
        }
    }
}
