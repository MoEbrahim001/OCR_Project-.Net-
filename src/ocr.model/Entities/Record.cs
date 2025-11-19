using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ocr.Model.Entities;

[Table("Records")]
public class Record
{
    [Key]
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? IdNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Address { get; set; }

    public string? Gender { get; set; }

    public string? Profession { get; set; }

    public string? MaritalStatus { get; set; }

    public string? Religion { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? PhotoBase64 { get; set; }
    public string? FaceBase64 { get; set; }


    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
