namespace Ocr.ViewModel;

public record RecordDto(
    int Id,
    string? Name,
    string? IdNumber,
    DateOnly? DateOfBirth,
    string? Address,
    string? Gender,
    string? Profession,
    string? MaritalStatus,
    string? Religion,
    DateOnly? EndDate,
    string? PhotoBase64,
    string? FaceBase64,
    string? Notes,
    DateTime CreatedAtUtc);
