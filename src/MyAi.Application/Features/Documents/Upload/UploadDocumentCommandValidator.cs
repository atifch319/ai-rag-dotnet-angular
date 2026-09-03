using FluentValidation;

namespace MyAi.Application.Features.Documents.Upload;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly string[] AllowedContentTypes =
    [
        "application/pdf",
        "text/plain"
    ];

    private static readonly string[] AllowedExtensions = [".pdf", ".txt"];

    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("File is required.");

        RuleFor(x => x.File.Length)
            .GreaterThan(0)
            .WithMessage("File must not be empty.")
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB.");

        RuleFor(x => x.File.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Only PDF (.pdf) and TXT (.txt) files are allowed.");

        RuleFor(x => x.File.FileName)
            .Must(fn => AllowedExtensions.Contains(Path.GetExtension(fn)?.ToLowerInvariant()))
            .WithMessage("Only .pdf and .txt file extensions are allowed.");
    }
}
