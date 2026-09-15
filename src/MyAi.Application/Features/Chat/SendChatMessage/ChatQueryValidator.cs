using FluentValidation;
using MyAi.Application.Features.Search.SearchDocumentChunks;

namespace MyAi.Application.Features.Chat.SendChatMessage;

public sealed class ChatQueryValidator : AbstractValidator<ChatQuery>
{
    public ChatQueryValidator()
    {
        RuleFor(x => x.Message)
            .Must(message => !string.IsNullOrWhiteSpace(message))
            .WithMessage("Message must not be empty.");

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, SearchDocumentChunksQueryValidator.MaxTopK)
            .WithMessage($"TopK must be between 1 and {SearchDocumentChunksQueryValidator.MaxTopK}.");
    }
}
