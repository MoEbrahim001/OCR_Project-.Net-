using Microsoft.Extensions.Options;
using Ocr.Domain.Storage;
using Ocr.Infrastructure.Storage;

namespace ocr.infrastructure.Storage;

public sealed class LocalImageStorage : IImageStorage
{
    private readonly string _rootPath;

    public LocalImageStorage(
        IOptions<ImageStorageOptions> options)
    {
        _rootPath =
            options.Value.RootPath;

        if (string.IsNullOrWhiteSpace(_rootPath))
        {
            throw new InvalidOperationException(
                "ImageStorage:RootPath is not configured."
            );
        }

        Directory.CreateDirectory(
            _rootPath
        );
    }

    public async Task<string> SaveAsync(
    byte[] data,
    string contentType,
    ImageSide side,
    CancellationToken ct = default)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException(
                "Image data is empty.",
                nameof(data)
            );
        }

        var extension =
            GetExtension(contentType);

        var now =
            DateTime.UtcNow;

        var sideFolder =
            side switch
            {
                ImageSide.Front => "Front",
                ImageSide.Back => "Back",

                _ => throw new InvalidOperationException(
                    "Invalid image side."
                )
            };

        var relativeFolder =
            Path.Combine(
                sideFolder,
                now.Year.ToString(),
                now.Month.ToString("00")
            );

        var physicalFolder =
            Path.Combine(
                _rootPath,
                relativeFolder
            );

        Directory.CreateDirectory(
            physicalFolder
        );

        var fileName =
            $"{Guid.NewGuid():N}{extension}";

        var fullPath =
            Path.Combine(
                physicalFolder,
                fileName
            );

        await File.WriteAllBytesAsync(
            fullPath,
            data,
            ct
        );

        var key =
            Path.Combine(
                relativeFolder,
                fileName
            );

        return key.Replace('\\', '/');
    }


    public Task<StoredImage?> GetAsync(
        string key,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<StoredImage?>(
                null
            );
        }

        var fullPath =
            BuildSafePath(
                key
            );

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<StoredImage?>(
                null
            );
        }

        Stream stream =
            new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous
            );

        var contentType =
            GetContentType(
                fullPath
            );

        return Task.FromResult<StoredImage?>(
            new StoredImage
            {
                Stream = stream,
                ContentType = contentType
            }
        );
    }


    public Task DeleteAsync(
        string key,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.CompletedTask;
        }

        var fullPath =
            BuildSafePath(
                key
            );

        if (File.Exists(fullPath))
        {
            File.Delete(
                fullPath
            );
        }

        return Task.CompletedTask;
    }


    private string BuildSafePath(
        string key)
    {
        var normalizedKey =
            key.Replace(
                '/',
                Path.DirectorySeparatorChar
            );

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    _rootPath,
                    normalizedKey
                )
            );

        var root =
            Path.GetFullPath(
                _rootPath
            );

        if (
            !fullPath.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                "Invalid image key."
            );
        }

        return fullPath;
    }


    private static string GetExtension(
        string contentType)
    {
        return contentType
            .ToLowerInvariant()
            switch
        {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",

            _ => throw new InvalidOperationException(
                $"Unsupported image type: {contentType}"
            )
        };
    }


    private static string GetContentType(
        string path)
    {
        return Path
            .GetExtension(path)
            .ToLowerInvariant()
            switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",

            _ => "application/octet-stream"
        };
    }
}