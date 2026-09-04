using MediatR;

namespace MyAi.Application.Features.Documents.ExtractText;

public sealed record ExtractDocumentTextCommand(long Id) : IRequest<ExtractDocumentTextResponse>;
