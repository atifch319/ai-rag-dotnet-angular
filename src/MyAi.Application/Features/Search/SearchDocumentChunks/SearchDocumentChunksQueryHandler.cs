using MediatR;
using Microsoft.Extensions.Options;
using MyAi.Application.Abstractions.Embeddings;
using MyAi.Application.Abstractions.Search;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Configuration;

namespace MyAi.Application.Features.Search.SearchDocumentChunks;

public sealed class SearchDocumentChunksQueryHandler
    : IRequestHandler<SearchDocumentChunksQuery, SearchDocumentChunksResponse>
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly OpenAIOptions _openAiOptions;

    public SearchDocumentChunksQueryHandler(
        IEmbeddingService embeddingService,
        ISemanticSearchService semanticSearchService,
        IOptions<OpenAIOptions> openAiOptions)
    {
        _embeddingService = embeddingService;
        _semanticSearchService = semanticSearchService;
        _openAiOptions = openAiOptions.Value;
    }

    public async Task<SearchDocumentChunksResponse> Handle(
        SearchDocumentChunksQuery request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        float[] queryEmbedding;
        try
        {
            queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                request.Query,
                cancellationToken);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ValidationException("OpenAI", "Embedding generation failed.");
        }

        if (queryEmbedding is null || queryEmbedding.Length != _openAiOptions.EmbeddingDimensions)
        {
            throw new ValidationException("Embedding", "Embedding generation failed.");
        }

        IReadOnlyList<SemanticSearchMatch> matches;
        try
        {
            matches = await _semanticSearchService.SearchByCosineDistanceAsync(
                queryEmbedding,
                request.TopK,
                cancellationToken);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ValidationException("Search", "Semantic search failed.");
        }

        var results = matches
            .Select(match => new SemanticSearchResult(
                match.ChunkId,
                match.DocumentId,
                match.ChunkIndex,
                match.Content,
                Similarity: 1d - match.CosineDistance))
            .OrderByDescending(result => result.Similarity)
            .ToList();

        return new SearchDocumentChunksResponse(
            request.Query,
            request.TopK,
            results.Count,
            results);
    }
}
