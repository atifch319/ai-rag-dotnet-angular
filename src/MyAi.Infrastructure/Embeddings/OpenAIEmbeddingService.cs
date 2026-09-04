using System.ClientModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyAi.Application.Abstractions.Embeddings;
using MyAi.Application.Common.Exceptions;
using MyAi.Application.Configuration;
using OpenAI.Embeddings;

namespace MyAi.Infrastructure.Embeddings;

public sealed class OpenAIEmbeddingService : IEmbeddingService
{
    private const int MaxBatchSize = 64;

    private readonly OpenAIOptions _options;
    private readonly ILogger<OpenAIEmbeddingService> _logger;
    private readonly Lazy<EmbeddingClient> _client;

    public OpenAIEmbeddingService(
        IOptions<OpenAIOptions> options,
        ILogger<OpenAIEmbeddingService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<EmbeddingClient>(CreateClient);
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var embeddings = await GenerateEmbeddingsAsync([text], cancellationToken);
        return embeddings[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (texts is null || texts.Count == 0)
        {
            throw new ValidationException("Text", "At least one text value is required to generate embeddings.");
        }

        if (texts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ValidationException("Text", "Chunk content must not be empty.");
        }

        var dimensions = _options.EmbeddingDimensions > 0 ? _options.EmbeddingDimensions : 1536;
        var results = new float[texts.Count][];
        var embeddingOptions = new EmbeddingGenerationOptions { Dimensions = dimensions };

        for (var offset = 0; offset < texts.Count; offset += MaxBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchSize = Math.Min(MaxBatchSize, texts.Count - offset);
            var batch = texts.Skip(offset).Take(batchSize).ToList();

            try
            {
                var response = await _client.Value.GenerateEmbeddingsAsync(
                    batch,
                    embeddingOptions,
                    cancellationToken);

                foreach (var embedding in response.Value)
                {
                    var vector = embedding.ToFloats().ToArray();
                    if (vector.Length != dimensions)
                    {
                        throw new ValidationException(
                            "Embedding",
                            "Embedding generation failed.");
                    }

                    results[offset + embedding.Index] = vector;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "OpenAI embedding generation failed for model {EmbeddingModel}.",
                    _options.EmbeddingModel);
                throw new ValidationException("OpenAI", "Embedding generation failed.");
            }
        }

        if (results.Any(vector => vector is null))
        {
            throw new ValidationException("OpenAI", "Embedding generation failed.");
        }

        return results!;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new ValidationException("OpenAI", "OpenAI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.EmbeddingModel))
        {
            throw new ValidationException("OpenAI", "OpenAI embedding model is not configured.");
        }
    }

    private EmbeddingClient CreateClient()
    {
        EnsureConfigured();
        return new EmbeddingClient(_options.EmbeddingModel, new ApiKeyCredential(_options.ApiKey));
    }
}
