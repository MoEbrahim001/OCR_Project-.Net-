using Ocr.Domain.Dtos;

namespace Ocr.Domain.Services
{
    public interface IOcrClient
    {
        Task<OcrExtraction> ExtractFrontAsync(
        Stream imageStream, string fileName, string? contentType, int? threshold = null, CancellationToken ct = default);

        Task<OcrExtraction> ExtractBackAsync(
            Stream imageStream, string fileName, string? contentType, int? threshold = null, CancellationToken ct = default);
    }
}
