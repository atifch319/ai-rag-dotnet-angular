using MediatR;
using MyAi.Application.Abstractions.Rag;

namespace MyAi.Application.Features.Chat.SendChatMessage;

public sealed class ChatQueryHandler : IRequestHandler<ChatQuery, ChatResponse>
{
    private readonly IRagService _ragService;

    public ChatQueryHandler(IRagService ragService)
    {
        _ragService = ragService;
    }

    public async Task<ChatResponse> Handle(ChatQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rag = await _ragService.AskAsync(request.Message, request.TopK, cancellationToken);

        return new ChatResponse(rag.Query, rag.Answer, rag.Sources);
    }
}
