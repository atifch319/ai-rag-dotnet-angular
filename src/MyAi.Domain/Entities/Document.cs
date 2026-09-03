namespace MyAi.Domain.Entities;

public class Document
{
    public long Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; set; }

    public string Status { get; set; } = string.Empty;
}
