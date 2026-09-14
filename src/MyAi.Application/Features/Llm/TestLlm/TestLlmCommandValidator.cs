using FluentValidation;

namespace MyAi.Application.Features.Llm.TestLlm;

public sealed class TestLlmCommandValidator : AbstractValidator<TestLlmCommand>
{
    public TestLlmCommandValidator()
    {
        RuleFor(x => x.Prompt)
            .Must(prompt => !string.IsNullOrWhiteSpace(prompt))
            .WithMessage("Prompt must not be empty.");
    }
}
