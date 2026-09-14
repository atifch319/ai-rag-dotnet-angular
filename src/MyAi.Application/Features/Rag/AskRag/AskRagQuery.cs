using MediatR;

namespace MyAi.Application.Features.Rag.AskRag;

public sealed record AskRagQuery(string Query, int TopK = 5) : IRequest<AskRagResponse>;
