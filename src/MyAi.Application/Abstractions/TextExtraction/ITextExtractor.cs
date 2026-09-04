namespace MyAi.Application.Abstractions.TextExtraction;

public interface ITextExtractor
{
    Task<string> ExtractTextAsync(
        string filePath,
        string contentType,
        CancellationToken cancellationToken = default);
}
