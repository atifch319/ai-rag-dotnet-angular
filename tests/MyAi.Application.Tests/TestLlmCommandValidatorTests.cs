using MyAi.Application.Features.Llm.TestLlm;

namespace MyAi.Application.Tests;

public sealed class TestLlmCommandValidatorTests
{
    private readonly TestLlmCommandValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyPrompt_IsInvalid(string? prompt)
    {
        var result = _validator.Validate(new TestLlmCommand(prompt!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Prompt");
    }

    [Fact]
    public void Validate_ValidPrompt_IsValid()
    {
        var result = _validator.Validate(new TestLlmCommand("Explain semantic search in one paragraph."));

        Assert.True(result.IsValid);
    }
}
