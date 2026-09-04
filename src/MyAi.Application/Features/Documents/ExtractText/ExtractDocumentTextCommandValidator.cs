using FluentValidation;

namespace MyAi.Application.Features.Documents.ExtractText;

public sealed class ExtractDocumentTextCommandValidator : AbstractValidator<ExtractDocumentTextCommand>
{
    public ExtractDocumentTextCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Document id must be greater than 0.");
    }
}
