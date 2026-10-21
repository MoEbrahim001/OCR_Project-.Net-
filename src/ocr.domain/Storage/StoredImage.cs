namespace Ocr.Domain.Storage;

public sealed class StoredImage
{
    public required Stream Stream { get; init; }

    public required string ContentType { get; init; }
}