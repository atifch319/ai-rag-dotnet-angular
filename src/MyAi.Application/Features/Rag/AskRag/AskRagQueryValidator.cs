using FluentValidation;
using MyAi.Application.Features.Search.SearchDocumentChunks;

namespace MyAi.Application.Features.Rag.AskRag;

public sealed class AskRagQueryValidator : AbstractValidator<AskRagQuery>
{
    public AskRagQueryValidator()
    {
        RuleFor(x => x.Query)
            .Must(query => !string.IsNullOrWhiteSpace(query))
            .WithMessage("Query must not be empty.");

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, SearchDocumentChunksQueryValidator.MaxTopK)
            .WithMessage($"TopK must be between 1 and {SearchDocumentChunksQueryValidator.MaxTopK}.");
    }
}
