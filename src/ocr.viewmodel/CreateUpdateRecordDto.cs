using System.ComponentModel.DataAnnotations;

namespace Ocr.ViewModel;

public class CreateUpdateRecordDto
{
    public string? Name { get; set; }
    public string? IdNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }   // DateOnly? ?????? ?? ???????
    public string? Address { get; set; }
    public string? Gender { get; set; }
    public string? Profession { get; set; }      // ?????? ?? ??????? ???????
    public string? MaritalStatus { get; set; }
    public string? Religion { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? PhotoBase64 { get; set; }
    public string? FaceBase64 { get; set; }
    public string? Notes { get; set; }
    public int? Age { get; set; }
    public string? HusbandName { get; set; } 
}
