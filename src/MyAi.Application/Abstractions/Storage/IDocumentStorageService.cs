using Microsoft.AspNetCore.Http;

namespace MyAi.Application.Abstractions.Storage;

public interface IDocumentStorageService
{
    Task<string> StoreAsync(IFormFile file, CancellationToken cancellationToken = default);
}
