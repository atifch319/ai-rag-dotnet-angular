using MyAi.Application.Features.Rag.AskRag;
using MyAi.Application.Features.Search.SearchDocumentChunks;

namespace MyAi.Application.Tests;

public sealed class AskRagQueryValidatorTests
{
    private readonly AskRagQueryValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyQuery_IsInvalid(string? query)
    {
        var result = _validator.Validate(new AskRagQuery(query!, TopK: 5));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Query");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Validate_InvalidTopK_IsInvalid(int topK)
    {
        var result = _validator.Validate(new AskRagQuery("When will my refund be processed?", topK));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "TopK");
    }

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var result = _validator.Validate(new AskRagQuery("When will my refund be processed?", 5));

        Assert.True(result.IsValid);
        Assert.Equal(20, SearchDocumentChunksQueryValidator.MaxTopK);
    }
}
