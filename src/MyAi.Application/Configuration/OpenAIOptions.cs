namespace MyAi.Application.Configuration;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;

    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Output dimensions for text-embedding-3-small. The model default is 1536.
    /// </summary>
    public int EmbeddingDimensions { get; set; } = 1536;
}
