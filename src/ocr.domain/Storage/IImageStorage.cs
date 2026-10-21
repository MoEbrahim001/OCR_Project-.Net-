namespace Ocr.Domain.Storage;

public interface IImageStorage
{
    Task<string> SaveAsync(
        byte[] data,
        string contentType,
        ImageSide side,
        CancellationToken ct = default
    );

    Task<StoredImage?> GetAsync(
        string key,
        CancellationToken ct = default
    );

    Task DeleteAsync(
        string key,
        CancellationToken ct = default
    );
}