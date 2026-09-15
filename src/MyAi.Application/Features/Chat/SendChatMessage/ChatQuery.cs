using MediatR;
using MyAi.Application.Features.Rag.AskRag;

namespace MyAi.Application.Features.Chat.SendChatMessage;

public sealed record ChatQuery(string Message, int TopK = 5) : IRequest<ChatResponse>;

public sealed record ChatResponse(
    string Message,
    string Answer,
    IReadOnlyList<RagSource> Sources);
