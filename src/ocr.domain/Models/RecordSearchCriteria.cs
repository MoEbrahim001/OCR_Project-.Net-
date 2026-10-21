namespace Ocr.Domain.Models;

public class RecordSearchCriteria
{
    public string? Name { get; set; }

    public string? IdNumber { get; set; }

    public string? Address { get; set; }

    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public int? Age { get; set; }

    public string? Religion { get; set; }

    public string? Profession { get; set; }

    public string? MaritalStatus { get; set; }

    public string? HusbandName { get; set; }

    public DateOnly? EndDate { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}