using MediatR;
using Microsoft.Extensions.Options;
using MyAi.Application.Abstractions.Chat;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Configuration;

namespace MyAi.Application.Features.Llm.TestLlm;

public sealed class TestLlmCommandHandler : IRequestHandler<TestLlmCommand, TestLlmResponse>
{
    private const string SystemInstructions = "You are a helpful assistant. Answer clearly and concisely.";

    private readonly IChatCompletionService _chatCompletionService;
    private readonly OpenAIOptions _openAiOptions;

    public TestLlmCommandHandler(
        IChatCompletionService chatCompletionService,
        IOptions<OpenAIOptions> openAiOptions)
    {
        _chatCompletionService = chatCompletionService;
        _openAiOptions = openAiOptions.Value;
    }

    public async Task<TestLlmResponse> Handle(TestLlmCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string answer;
        try
        {
            answer = await _chatCompletionService.CompleteAsync(
                request.Prompt,
                SystemInstructions,
                cancellationToken);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ValidationException("OpenAI", "Chat completion failed.");
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new ValidationException("OpenAI", "Chat completion failed.");
        }

        return new TestLlmResponse(request.Prompt, answer, _openAiOptions.ChatModel);
    }
}
