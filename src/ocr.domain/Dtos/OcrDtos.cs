namespace Ocr.Domain.Dtos
{
    public record FrontOcrResult(
        string? Name,
        string? NationalId,
        string? Address,
        string? Dob,
        int? Age
    );

    public record BackOcrResult(
        string? proffession,
        string? Gender,
        string? Religion,
        string? MaritalStatus,
        string? HusbandName,
        string? ExpiryDate
    );

    public sealed class OcrExtraction
    {
        public OcrExtraction(string rawJson) => RawJson = rawJson;
        public string RawJson { get; }
    }
}
