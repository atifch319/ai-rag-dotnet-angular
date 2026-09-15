using MediatR;
using MyAi.Application.Abstractions.Rag;

namespace MyAi.Application.Features.Rag.AskRag;

public sealed class AskRagQueryHandler : IRequestHandler<AskRagQuery, AskRagResponse>
{
    private readonly IRagService _ragService;

    public AskRagQueryHandler(IRagService ragService)
    {
        _ragService = ragService;
    }

    public Task<AskRagResponse> Handle(AskRagQuery request, CancellationToken cancellationToken)
    {
        return _ragService.AskAsync(request.Query, request.TopK, cancellationToken);
    }
}
