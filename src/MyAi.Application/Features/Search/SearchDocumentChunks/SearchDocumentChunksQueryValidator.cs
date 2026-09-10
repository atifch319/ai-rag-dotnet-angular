using FluentValidation;

namespace MyAi.Application.Features.Search.SearchDocumentChunks;

public sealed class SearchDocumentChunksQueryValidator : AbstractValidator<SearchDocumentChunksQuery>
{
    public const int MaxTopK = 20;

    public SearchDocumentChunksQueryValidator()
    {
        RuleFor(x => x.Query)
            .Must(query => !string.IsNullOrWhiteSpace(query))
            .WithMessage("Query must not be empty.");

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, MaxTopK)
            .WithMessage($"TopK must be between 1 and {MaxTopK}.");
    }
}
