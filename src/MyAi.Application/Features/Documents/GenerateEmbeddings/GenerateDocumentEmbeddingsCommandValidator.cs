using FluentValidation;

namespace MyAi.Application.Features.Documents.GenerateEmbeddings;

public sealed class GenerateDocumentEmbeddingsCommandValidator
    : AbstractValidator<GenerateDocumentEmbeddingsCommand>
{
    public GenerateDocumentEmbeddingsCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Document id must be greater than 0.");
    }
}
