using MyAi.Application.Features.Rag.AskRag;

namespace MyAi.Application.Abstractions.Rag;

public interface IRagService
{
    Task<AskRagResponse> AskAsync(
        string query,
        int topK,
        CancellationToken cancellationToken = default);
}
