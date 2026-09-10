using MediatR;

namespace MyAi.Application.Features.Search.SearchDocumentChunks;

public sealed record SearchDocumentChunksQuery(string Query, int TopK = 5)
    : IRequest<SearchDocumentChunksResponse>;
