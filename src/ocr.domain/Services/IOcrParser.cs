using Ocr.Domain.Dtos;

namespace Ocr.Domain.Services
{
    public interface IOcrParser
    {
        FrontOcrResult ParseFront(OcrExtraction raw);
        BackOcrResult ParseBack(OcrExtraction raw);
    }
}
