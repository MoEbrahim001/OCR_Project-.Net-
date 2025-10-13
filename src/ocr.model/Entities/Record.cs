using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ocr.Model.Entities;

[Table("Records")]
public class Record
{
    [Key]
    public int Id { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(50)]
    public string? IdNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? Gender { get; set; }

    [MaxLength(200)]
    public string? Profession { get; set; }

    [MaxLength(100)]
    public string? MaritalStatus { get; set; }

    [MaxLength(100)]
    public string? Religion { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? PhotoBase64 { get; set; }
    public string? FaceBase64 { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
