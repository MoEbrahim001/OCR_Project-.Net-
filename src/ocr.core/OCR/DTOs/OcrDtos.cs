namespace Ocr.Core.OCR.DTOs
{
    public record FrontOcrResult(
     string? Name,
     string? NationalId,
     string? Address,
     string? Governorate,
     string? Dob,
     int? Age
 );
    public record BackOcrResult(
        string? proffession,
        string? Gender,
        string? Religion,
        string? MaritalStatus,
        string? HusbandName,
        string? ExpiryDate,
        string? NationalId = null
    );

    public sealed class OcrExtraction
    {
        public OcrExtraction(string rawJson) => RawJson = rawJson;
        public string RawJson { get; }
    }
}
