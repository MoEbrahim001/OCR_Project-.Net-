
using Ocr.Core.OCR.DTOs;

namespace Ocr.Core.Abstractions
{
    public interface IOcrClient
    {
        Task<OcrExtraction> ExtractFrontAsync(
            Stream imageStream,
            string fileName,
            string? contentType,
            int threshold,
            CancellationToken ct = default);

        Task<OcrExtraction> ExtractBackAsync(
            Stream imageStream,
            string fileName,
            string? contentType,
            int threshold,
            CancellationToken ct = default);
    }
}