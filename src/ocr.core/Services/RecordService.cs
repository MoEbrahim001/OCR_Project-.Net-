using Microsoft.EntityFrameworkCore;
using Ocr.Domain.Services;
using Ocr.Model.Entities;
using Ocr.ViewModel;
using Ocr.Domain.UnitOfWork;
using Ocr.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using ocr.viewmodel;
using System.Text.Json;
using System.Text;

namespace Ocr.Core.Services;

public class RecordService : IRecordService
{
    private readonly IUnitOfWork _uow;
    private readonly IOcrClient _ocrClient;   // you already have this
    private readonly IRecordRepository _repo;
    public RecordService(IUnitOfWork uow, IOcrClient ocrClient, IRecordRepository repo) { _uow = uow; _ocrClient = ocrClient;
        _repo = repo;
    }
    private static DateOnly? ParseYmd(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;

        // Remove spaces, newlines, and carriage returns
        s = s.Trim()
             .Replace("\r", "")
             .Replace("\n", "")
             .Replace(" ", "")
             .Replace("--", "-");

        // Extract only digits and dashes
        var digits = new string(s.Where(c => char.IsDigit(c) || c == '-').ToArray());

        // If the OCR returned something like 2026-88-83, try to normalize it
        var parts = digits.Split('-', StringSplitOptions.RemoveEmptyEntries);

        try
        {
            if (parts.Length == 3)
            {
                // Force valid ranges for month/day
                int year = int.Parse(parts[0]);
                int month = Math.Clamp(int.Parse(parts[1]), 1, 12);
                int day = Math.Clamp(int.Parse(parts[2]), 1, 28); // Avoid invalid 31/30 issues

                return new DateOnly(year, month, day);
            }

            // Handle case like "20260823"
            if (digits.Length == 8 && !digits.Contains('-'))
            {
                int year = int.Parse(digits.Substring(0, 4));
                int month = int.Parse(digits.Substring(4, 2));
                int day = int.Parse(digits.Substring(6, 2));
                return new DateOnly(year, month, day);
            }
        }
        catch
        {
            // Fall back to today + 3 years if OCR gives nonsense
            return new DateOnly(DateTime.UtcNow.Year + 3, 1, 1);
        }

        return null;
    }


    public async Task<Record> ImportAsync(
     IFormFile frontImage,
     IFormFile backImage,
     int threshold,
     CancellationToken ct = default,
     CreateUpdateRecordDto? overrideDto = null)
    {
        // --- Call Python: FRONT ---
        using var f = frontImage.OpenReadStream();
        var frontExtraction = await _ocrClient.ExtractFrontAsync(
            f, frontImage.FileName, frontImage.ContentType ?? "application/octet-stream", threshold, ct);
        var front = JsonSerializer.Deserialize<FrontExtractDto>(frontExtraction.RawJson);

        // --- Call Python: BACK ---
        using var b = backImage.OpenReadStream();
        var backExtraction = await _ocrClient.ExtractBackAsync(
            b, backImage.FileName, backImage.ContentType ?? "application/octet-stream", threshold, ct);
        var back = JsonSerializer.Deserialize<BackExtractDto>(backExtraction.RawJson);

        // --- Map to entity ---
        var rec = new Record
        {
            Name = front?.name,
            IdNumber = front?.ID,
            DateOfBirth = ParseYmd(front?.DOB),
            Address = front?.address,
            Gender = back?.Gender,
            Profession = back?.Profession,
            MaritalStatus = back?.MaritalStatus,
            Religion = back?.Religion,
            EndDate = ParseYmd(back?.EndDate),   // ? fixes EndDate=null
            PhotoBase64 = front?.image,
            FaceBase64 = front?.face,
            Notes = null
        };

        await _repo.AddAsync(rec, ct);
        await _repo.SaveChangesAsync(ct);
        return rec;
    }
    private static RecordDto ToDto(Record r) => new(
        r.Id, r.Name, r.IdNumber, r.DateOfBirth, r.Address, r.Gender, r.Profession,
        r.MaritalStatus, r.Religion, r.EndDate, r.PhotoBase64, r.FaceBase64, r.Notes, r.CreatedAtUtc);

    public async Task<RecordDto?> GetAsync(int id)
    {
        var r = await _uow.Records.GetByIdAsync(id);
        return r is null ? null : ToDto(r);
    }

    public async Task<PagedResult<RecordDto>> GetPagedAsync(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 10;

        var q = _uow.Records.Query().AsNoTracking();

        // Earliest first (smallest Id first)
        q = q.OrderBy(r => r.Id);
        // If you want latest first instead, use:
        // q = q.OrderByDescending(r => r.Id);

        var total = await q.CountAsync();

        var items = await q
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => ToDto(r)) // or map after ToList if you prefer
            .ToListAsync();

        return new PagedResult<RecordDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total
        };
    }


    public async Task<RecordDto> CreateAsync(CreateUpdateRecordDto dto)
    {
        var r = new Record
        {
            Name = dto.Name, IdNumber = dto.IdNumber, DateOfBirth = dto.DateOfBirth,
            Address = dto.Address, Gender = dto.Gender,
            Profession = dto.Profession,
            MaritalStatus = dto.MaritalStatus, Religion = dto.Religion,
            EndDate = dto.EndDate, PhotoBase64 = dto.PhotoBase64, FaceBase64 = dto.FaceBase64, Notes = dto.Notes
        };
        await _uow.Records.AddAsync(r);
        await _uow.SaveChangesAsync();
        return ToDto(r);
    }

    public async Task<RecordDto?> UpdateAsync(int id, CreateUpdateRecordDto dto)
    {
        var r = await _uow.Records.GetByIdAsync(id);
        if (r is null) return null;
        r.Name = dto.Name;
        r.IdNumber = dto.IdNumber;
        r.DateOfBirth = dto.DateOfBirth;
        r.Address = dto.Address;
        r.Gender = dto.Gender;
        r.Profession = dto.Profession;
        r.MaritalStatus = dto.MaritalStatus;
        r.Religion = dto.Religion;
        r.EndDate = dto.EndDate;
        r.PhotoBase64 = dto.PhotoBase64;
        r.FaceBase64 = dto.FaceBase64;
        r.Notes = dto.Notes;

        await _uow.Records.UpdateAsync(r);
        await _uow.SaveChangesAsync();
        return ToDto(r);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var r = await _uow.Records.GetByIdAsync(id);
        if (r is null) return false;
        await _uow.Records.DeleteAsync(r);
        await _uow.SaveChangesAsync();
        return true;
    }
    public async Task<PagedResult<RecordDto>> SearchAsync(
        string? name, string? idNumber, int pageNumber, int pageSize)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 10;

        var q = _uow.Records.Query().AsNoTracking(); // IQueryable<Record> from repo :contentReference[oaicite:2]{index=2}

        var nameTerm = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var idTermRaw = string.IsNullOrWhiteSpace(idNumber) ? null : idNumber.Trim();
        var idTerm = NormalizeDigits(idTermRaw); // convert Arabic digits to 0-9

        if (nameTerm is not null)
            q = q.Where(r => r.Name != null && EF.Functions.Like(r.Name, $"%{nameTerm}%"));

        if (!string.IsNullOrEmpty(idTerm))
        {
            // Match either raw or space-stripped DB value
            q = q.Where(r =>
                r.IdNumber != null &&
                (
                  r.IdNumber.Contains(idTerm) ||
                  EF.Functions.Like(r.IdNumber, $"%{idTerm}%") ||
                  // translate to SQL: REPLACE(IdNumber, ' ', '')
                  r.IdNumber.Replace(" ", "").Contains(idTerm)
                )
            );
        }

        var total = await q.CountAsync();

        var items = await q
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => ToDto(r)) // your existing mapper to RecordDto :contentReference[oaicite:3]{index=3}
            .ToListAsync();

        return new PagedResult<RecordDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    // Runs on server side (outside the LINQ expression)
    private static string? NormalizeDigits(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            // Arabic-Indic ?????????? (U+0660..U+0669)
            if (ch >= '\u0660' && ch <= '\u0669')
                sb.Append((char)('0' + (ch - '\u0660')));
            // Extended Arabic-Indic ?????????? (U+06F0..U+06F9)
            else if (ch >= '\u06F0' && ch <= '\u06F9')
                sb.Append((char)('0' + (ch - '\u06F0')));
            else
                sb.Append(ch);
        }
        // strip inner spaces just in the query side
        return sb.ToString().Replace(" ", "");
    }

}
