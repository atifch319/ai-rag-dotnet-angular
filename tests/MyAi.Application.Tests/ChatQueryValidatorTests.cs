using MyAi.Application.Features.Chat.SendChatMessage;
using MyAi.Application.Features.Search.SearchDocumentChunks;

namespace MyAi.Application.Tests;

public sealed class ChatQueryValidatorTests
{
    private readonly ChatQueryValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyMessage_IsInvalid(string? message)
    {
        var result = _validator.Validate(new ChatQuery(message!, TopK: 5));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Message");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Validate_InvalidTopK_IsInvalid(int topK)
    {
        var result = _validator.Validate(new ChatQuery("What is semantic search?", topK));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "TopK");
    }

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        var result = _validator.Validate(new ChatQuery("What is semantic search?", 5));

        Assert.True(result.IsValid);
        Assert.Equal(20, SearchDocumentChunksQueryValidator.MaxTopK);
    }
}
