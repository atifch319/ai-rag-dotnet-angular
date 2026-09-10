using MyAi.Application.Features.Search.SearchDocumentChunks;

namespace MyAi.Application.Tests;

public sealed class SearchDocumentChunksQueryValidatorTests
{
    private readonly SearchDocumentChunksQueryValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyQuery_IsInvalid(string? query)
    {
        var result = _validator.Validate(new SearchDocumentChunksQuery(query!, TopK: 5));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Query");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Validate_InvalidTopK_IsInvalid(int topK)
    {
        var result = _validator.Validate(new SearchDocumentChunksQuery("How can I get a refund?", topK));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "TopK");
    }

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var result = _validator.Validate(new SearchDocumentChunksQuery("How can I get a refund?", 5));

        Assert.True(result.IsValid);
    }
}
