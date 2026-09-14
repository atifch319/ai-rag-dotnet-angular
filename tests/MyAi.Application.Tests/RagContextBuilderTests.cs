using MyAi.Application.Features.Rag;

namespace MyAi.Application.Tests;

public sealed class RagContextBuilderTests
{
    [Fact]
    public void Build_FormatsSourceMetadataAndContent()
    {
        var context = RagContextBuilder.Build(
        [
            new RagContextChunk("refund-policy.pdf", 5, 3, "Refunds are processed within 7 business days."),
            new RagContextChunk("refund-policy.pdf", 5, 4, "Approval is required first.")
        ]);

        Assert.Contains("[Source: refund-policy.pdf | DocumentId: 5 | Chunk: 3]", context);
        Assert.Contains("Refunds are processed within 7 business days.", context);
        Assert.Contains("[Source: refund-policy.pdf | DocumentId: 5 | Chunk: 4]", context);
        Assert.Contains("Approval is required first.", context);
        Assert.DoesNotContain("embedding", context, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_EmptyChunks_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, RagContextBuilder.Build([]));
    }
}
