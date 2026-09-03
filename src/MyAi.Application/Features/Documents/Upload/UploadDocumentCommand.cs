using MediatR;
using Microsoft.AspNetCore.Http;

namespace MyAi.Application.Features.Documents.Upload;

public sealed record UploadDocumentCommand(IFormFile File) : IRequest<DocumentUploadResponse>;
