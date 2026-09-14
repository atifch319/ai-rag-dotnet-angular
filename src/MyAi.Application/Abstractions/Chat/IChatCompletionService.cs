namespace MyAi.Application.Abstractions.Chat;

public interface IChatCompletionService
{
    Task<string> CompleteAsync(
        string userPrompt,
        string? systemInstructions,
        CancellationToken cancellationToken = default);
}
