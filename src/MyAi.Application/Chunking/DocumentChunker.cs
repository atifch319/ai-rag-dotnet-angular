using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MyAi.Application.Abstractions.Chunking;
using MyAi.Application.Configuration;

namespace MyAi.Application.Chunking;

public sealed class DocumentChunker : IDocumentChunker
{
    private static readonly Regex WordRegex = new(@"\S+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly DocumentChunkingOptions _options;

    public DocumentChunker(IOptions<DocumentChunkingOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<string> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var words = WordRegex.Matches(text)
            .Select(match => (Start: match.Index, End: match.Index + match.Length))
            .ToList();

        if (words.Count == 0)
        {
            return [];
        }

        var chunkSize = Math.Max(1, _options.ChunkSize);
        var overlap = Math.Clamp(_options.ChunkOverlap, 0, chunkSize - 1);
        var chunks = new List<string>();
        var start = 0;

        while (start < words.Count)
        {
            var endExclusive = Math.Min(start + chunkSize, words.Count);
            endExclusive = AdjustToTextBoundary(text, words, start, endExclusive);

            var chunk = text[words[start].Start..words[endExclusive - 1].End].Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            if (endExclusive >= words.Count)
            {
                break;
            }

            var nextStart = endExclusive - overlap;
            start = nextStart <= start ? endExclusive : nextStart;
        }

        return chunks;
    }

    private static int AdjustToTextBoundary(
        string text,
        IReadOnlyList<(int Start, int End)> words,
        int start,
        int endExclusive)
    {
        if (endExclusive >= words.Count)
        {
            return endExclusive;
        }

        var searchLimit = Math.Max(start + 1, endExclusive - Math.Max(1, (endExclusive - start) / 4));
        for (var index = endExclusive - 1; index >= searchLimit; index--)
        {
            var word = text[words[index].Start..words[index].End];
            if (EndsWithSentenceBoundary(word))
            {
                return index + 1;
            }
        }

        return endExclusive;
    }

    private static bool EndsWithSentenceBoundary(string word)
    {
        return word.EndsWith('.')
            || word.EndsWith('!')
            || word.EndsWith('?')
            || word.EndsWith(".\")")
            || word.EndsWith(".\"")
            || word.EndsWith(".'");
    }
}
