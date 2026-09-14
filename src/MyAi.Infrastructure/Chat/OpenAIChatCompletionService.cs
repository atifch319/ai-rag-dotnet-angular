using System.ClientModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyAi.Application.Abstractions.Chat;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Configuration;
using OpenAI.Chat;

namespace MyAi.Infrastructure.Chat;

public sealed class OpenAIChatCompletionService : IChatCompletionService
{
    private readonly OpenAIOptions _options;
    private readonly ILogger<OpenAIChatCompletionService> _logger;
    private readonly Lazy<ChatClient> _client;

    public OpenAIChatCompletionService(
        IOptions<OpenAIOptions> options,
        ILogger<OpenAIChatCompletionService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<ChatClient>(CreateClient);
    }

    public async Task<string> CompleteAsync(
        string userPrompt,
        string? systemInstructions,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ValidationException("Prompt", "Prompt must not be empty.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(systemInstructions))
            {
                messages.Add(new SystemChatMessage(systemInstructions));
            }

            messages.Add(new UserChatMessage(userPrompt));

            var completion = await _client.Value.CompleteChatAsync(messages, cancellationToken: cancellationToken);
            var answer = string.Concat(
                completion.Value.Content
                    .Where(part => !string.IsNullOrEmpty(part.Text))
                    .Select(part => part.Text));

            if (string.IsNullOrWhiteSpace(answer))
            {
                throw new ValidationException("OpenAI", "Chat completion failed.");
            }

            return answer;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "OpenAI chat completion failed for model {ChatModel}.",
                _options.ChatModel);
            throw new ValidationException("OpenAI", "Chat completion failed.");
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new ValidationException("OpenAI", "OpenAI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.ChatModel))
        {
            throw new ValidationException("OpenAI", "OpenAI chat model is not configured.");
        }
    }

    private ChatClient CreateClient()
    {
        EnsureConfigured();
        return new ChatClient(_options.ChatModel, new ApiKeyCredential(_options.ApiKey));
    }
}
