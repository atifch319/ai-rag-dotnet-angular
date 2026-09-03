using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using MyAi.Application.Abstractions.Storage;

namespace MyAi.Infrastructure.Storage;

public sealed class LocalDocumentStorageService : IDocumentStorageService
{
    private const string UploadFolder = "uploads/documents";
    private readonly string _basePath;

    public LocalDocumentStorageService(IWebHostEnvironment environment)
    {
        _basePath = Path.Combine(environment.WebRootPath, UploadFolder);
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> StoreAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(_basePath, storedFileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);

        return Path.Combine(UploadFolder, storedFileName).Replace("\\", "/");
    }
}
