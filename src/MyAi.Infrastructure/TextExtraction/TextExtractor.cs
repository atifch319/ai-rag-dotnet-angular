using System.Text;
using Microsoft.AspNetCore.Hosting;
using MyAi.Application.Abstractions.TextExtraction;
using MyAi.Application.Common.Exceptions;
using UglyToad.PdfPig;

namespace MyAi.Infrastructure.TextExtraction;

public sealed class TextExtractor : ITextExtractor
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "text/plain"
    };

    private readonly IWebHostEnvironment _environment;

    public TextExtractor(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> ExtractTextAsync(
        string filePath,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var mediaType = GetMediaType(contentType);
        if (!AllowedContentTypes.Contains(mediaType))
        {
            throw new ValidationException(
                "ContentType",
                "Only PDF (.pdf) and TXT (.txt) files are supported for text extraction.");
        }

        var fullPath = ResolveFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new ValidationException("FilePath", "The stored document file was not found.");
        }

        if (string.Equals(mediaType, "text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return await File.ReadAllTextAsync(fullPath, Encoding.UTF8, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ExtractPdfText(fullPath, cancellationToken);
    }

    private string ResolveFullPath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            return filePath;
        }

        return Path.GetFullPath(Path.Combine(_environment.WebRootPath, filePath));
    }

    private static string GetMediaType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var separatorIndex = contentType.IndexOf(';');
        return separatorIndex >= 0
            ? contentType[..separatorIndex].Trim()
            : contentType.Trim();
    }

    private static string ExtractPdfText(string fullPath, CancellationToken cancellationToken)
    {
        using var pdf = PdfDocument.Open(fullPath);
        var textBuilder = new StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageText = page.Text;
            if (string.IsNullOrWhiteSpace(pageText))
            {
                continue;
            }

            if (textBuilder.Length > 0)
            {
                textBuilder.AppendLine();
            }

            textBuilder.Append(pageText.Trim());
        }

        return textBuilder.ToString();
    }
}
