using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Ocr.Domain.Services;
using ocr.viewmodel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ocr.core.Services
{
    public class IdCardOcrService
    {
        private readonly IOcrClient _ocrClient;

        public IdCardOcrService(IOcrClient ocrClient)
        {
            _ocrClient = ocrClient;
        }

        public async Task<FrontExtractDto> ExtractFrontAsync(IFormFile file, int threshold, CancellationToken ct = default)
        {
            await using var stream = file.OpenReadStream();
            var extraction = await _ocrClient.ExtractFrontAsync(stream, file.FileName, file.ContentType, threshold, ct);

            // OcrExtraction holds the raw JSON string from Python
            var dto = JsonConvert.DeserializeObject<FrontExtractDto>(extraction.RawJson);
            return dto!;
        }

        public async Task<BackExtractDto> ExtractBackAsync(IFormFile file, int threshold, CancellationToken ct = default)
        {
            await using var stream = file.OpenReadStream();
            var extraction = await _ocrClient.ExtractBackAsync(stream, file.FileName, file.ContentType, threshold, ct);

            var dto = JsonConvert.DeserializeObject<BackExtractDto>(extraction.RawJson);
            return dto!;
        }
    }
}
