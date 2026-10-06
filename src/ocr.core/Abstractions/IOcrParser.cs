using Ocr.Core.OCR.DTOs;

namespace Ocr.Core.Abstractions
{
    public interface IOcrParser
    {
        FrontOcrResult ParseFront(OcrExtraction extraction);
        BackOcrResult ParseBack(OcrExtraction extraction);
    }
}