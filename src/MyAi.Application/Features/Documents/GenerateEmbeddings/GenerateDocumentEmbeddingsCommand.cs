using MediatR;

namespace MyAi.Application.Features.Documents.GenerateEmbeddings;

public sealed record GenerateDocumentEmbeddingsCommand(long Id)
    : IRequest<GenerateDocumentEmbeddingsResponse>;
