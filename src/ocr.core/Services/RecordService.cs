using Microsoft.AspNetCore.Http;

using Ocr.Core.Abstractions;
using Ocr.Domain.Services;
using Ocr.Domain.UnitOfWork;
using Ocr.Model.Entities;
using Ocr.ViewModel;

using ocr.viewmodel;

using System.Text;
using System.Text.Json;

namespace Ocr.Core.Services;

public class RecordService : IRecordService
{
    private readonly IUnitOfWork _uow;
    private readonly IOcrClient _ocrClient;
    private readonly IOcrParser _parser;

    public RecordService(
        IUnitOfWork uow,
        IOcrClient ocrClient,
        IOcrParser parser)
    {
        _uow = uow;
        _ocrClient = ocrClient;
        _parser = parser;
    }


    // =====================================================
    // Helpers
    // =====================================================

    private static DateOnly? ParseYmd(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        s = s.Trim()
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace(" ", "")
            .Replace("--", "-");

        var digits = new string(
            s.Where(c => char.IsDigit(c) || c == '-')
             .ToArray());

        var parts = digits.Split(
            '-',
            StringSplitOptions.RemoveEmptyEntries);

        try
        {
            if (parts.Length == 3)
            {
                var year = int.Parse(parts[0]);

                var month = Math.Clamp(
                    int.Parse(parts[1]),
                    1,
                    12);

                var day = Math.Clamp(
                    int.Parse(parts[2]),
                    1,
                    28);

                return new DateOnly(
                    year,
                    month,
                    day);
            }

            if (digits.Length == 8 &&
                !digits.Contains('-'))
            {
                var year = int.Parse(
                    digits.Substring(0, 4));

                var month = int.Parse(
                    digits.Substring(4, 2));

                var day = int.Parse(
                    digits.Substring(6, 2));

                return new DateOnly(
                    year,
                    month,
                    day);
            }
        }
        catch
        {
            return new DateOnly(
                DateTime.UtcNow.Year + 3,
                1,
                1);
        }

        return null;
    }


    private static RecordDto ToDto(Record record)
    {
        return new RecordDto(
            record.Id,
            record.Name,
            record.IdNumber,
            record.DateOfBirth,
            record.Address,
            record.Gender,
            record.Profession,
            record.MaritalStatus,
            record.Religion,
            record.EndDate,
            record.PhotoBase64,
            record.FaceBase64,
            record.Notes,
            record.CreatedAtUtc,
            record.FrontImageDataUrl,
            record.BackImageDataUrl
        );
    }

    private static string? NormalizeDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var sb = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            // Arabic-Indic digits
            if (ch >= '\u0660' &&
                ch <= '\u0669')
            {
                sb.Append(
                    (char)('0' + (ch - '\u0660')));
            }

            // Extended Arabic-Indic digits
            else if (
                ch >= '\u06F0' &&
                ch <= '\u06F9')
            {
                sb.Append(
                    (char)('0' + (ch - '\u06F0')));
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb
            .ToString()
            .Replace(" ", "");
    }


    // =====================================================
    // OCR Import
    // =====================================================

    public async Task<Record> ImportAsync(
        IFormFile frontImage,
        IFormFile backImage,
        int threshold,
        CancellationToken ct = default,
        CreateUpdateRecordDto? overrideDto = null)
    {
        // -------------------------
        // Front OCR
        // -------------------------

        using var frontStream =
            frontImage.OpenReadStream();

        var frontExtraction =
            await _ocrClient.ExtractFrontAsync(
                frontStream,
                frontImage.FileName,
                frontImage.ContentType
                    ?? "application/octet-stream",
                threshold,
                ct);

        var frontSmart =
            _parser.ParseFront(frontExtraction);

        var frontRaw =
            JsonSerializer.Deserialize<FrontExtractDto>(
                frontExtraction.RawJson);


        // -------------------------
        // Back OCR
        // -------------------------

        using var backStream =
            backImage.OpenReadStream();

        var backExtraction =
            await _ocrClient.ExtractBackAsync(
                backStream,
                backImage.FileName,
                backImage.ContentType
                    ?? "application/octet-stream",
                threshold,
                ct);

        var backSmart =
            _parser.ParseBack(backExtraction);


        // -------------------------
        // Map OCR -> Entity
        // -------------------------
        var frontImageDataUrl =
    await ToDataUrlAsync(
        frontImage,
        ct
    );

        var backImageDataUrl =
            await ToDataUrlAsync(
                backImage,
                ct
            );
        var record = new Record
        {
            Name =
           frontSmart.Name
           ?? frontRaw?.name,

            IdNumber =
           frontSmart.NationalId
           ?? frontRaw?.ID,

            DateOfBirth =
           ParseYmd(
               frontSmart.Dob
               ?? frontRaw?.DOB
           ),

            Address =
           frontSmart.Address
           ?? frontRaw?.address,

            Gender =
           backSmart.Gender,

            Profession =
           backSmart.proffession,

            MaritalStatus =
           backSmart.MaritalStatus,

            Religion =
           backSmart.Religion,

            EndDate =
           ParseYmd(
               backSmart.ExpiryDate
           ),

            PhotoBase64 =
           frontRaw?.image,

            FaceBase64 =
           frontRaw?.face,

            Notes =
           null,

            FrontImageDataUrl =
           frontImageDataUrl,

            BackImageDataUrl =
           backImageDataUrl
        };


        await _uow.Records.AddAsync(
            record,
            ct);

        await _uow.SaveChangesAsync(ct);

        return record;
    }


    // =====================================================
    // Get By Id
    // =====================================================

    public async Task<RecordDto?> GetAsync(int id)
    {
        var record =
            await _uow.Records.GetByIdAsync(id);

        return record is null
            ? null
            : ToDto(record);
    }


    // =====================================================
    // Pagination
    // =====================================================

    public async Task<PagedResult<RecordDto>> GetPagedAsync(
        int pageNumber,
        int pageSize)
    {
        if (pageNumber <= 0)
        {
            pageNumber = 1;
        }

        if (pageSize <= 0 ||
            pageSize > 100)
        {
            pageSize = 10;
        }

        var result =
            await _uow.Records.GetPagedAsync(
                pageNumber,
                pageSize);

        return new PagedResult<RecordDto>
        {
            Items = result.Items
                .Select(ToDto)
                .ToList(),

            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = result.TotalCount
        };
    }


    // =====================================================
    // Create
    // =====================================================

    public async Task<RecordDto> CreateAsync(
     CreateUpdateRecordDto dto)
    {
        Console.WriteLine(
            $"FRONT RECEIVED: {dto.FrontImageDataUrl?.Length ?? 0}"
        );

        Console.WriteLine(
            $"BACK RECEIVED: {dto.BackImageDataUrl?.Length ?? 0}"
        );

        var record = new Record
        {
            Name = dto.Name,
            IdNumber = dto.IdNumber,
            DateOfBirth = dto.DateOfBirth,
            Address = dto.Address,
            Gender = dto.Gender,
            Profession = dto.Profession,
            MaritalStatus = dto.MaritalStatus,
            Religion = dto.Religion,
            EndDate = dto.EndDate,

            PhotoBase64 = dto.PhotoBase64,
            FaceBase64 = dto.FaceBase64,

            Notes = dto.Notes,

            FrontImageDataUrl =
                dto.FrontImageDataUrl,

            BackImageDataUrl =
                dto.BackImageDataUrl
        };


        Console.WriteLine(
            $"ENTITY FRONT: {record.FrontImageDataUrl?.Length ?? 0}"
        );

        Console.WriteLine(
            $"ENTITY BACK: {record.BackImageDataUrl?.Length ?? 0}"
        );


        await _uow.Records.AddAsync(
            record
        );

        await _uow.SaveChangesAsync();


        return ToDto(record);
    }
    private static async Task<string> ToDataUrlAsync(
    IFormFile file,
    CancellationToken ct)
    {
        using var ms =
            new MemoryStream();

        await file.CopyToAsync(
            ms,
            ct
        );

        var base64 =
            Convert.ToBase64String(
                ms.ToArray()
            );

        var contentType =
            string.IsNullOrWhiteSpace(file.ContentType)
                ? "image/jpeg"
                : file.ContentType;

        return
            $"data:{contentType};base64,{base64}";
    }

    // =====================================================
    // Update
    // =====================================================

    public async Task<RecordDto?> UpdateAsync(
        int id,
        CreateUpdateRecordDto dto)
    {
        var record =
            await _uow.Records.GetByIdAsync(id);

        if (record is null)
        {
            return null;
        }

        record.Name = dto.Name;
        record.IdNumber = dto.IdNumber;
        record.DateOfBirth = dto.DateOfBirth;
        record.Address = dto.Address;
        record.Gender = dto.Gender;
        record.Profession = dto.Profession;
        record.MaritalStatus = dto.MaritalStatus;
        record.Religion = dto.Religion;
        record.EndDate = dto.EndDate;
        record.PhotoBase64 = dto.PhotoBase64;
        record.FaceBase64 = dto.FaceBase64;
        record.Notes = dto.Notes;
        record.FrontImageDataUrl = dto.FrontImageDataUrl;
        record.BackImageDataUrl = dto.BackImageDataUrl;

        _uow.Records.Update(record);

        await _uow.SaveChangesAsync();

        return ToDto(record);
    }


    // =====================================================
    // Delete
    // =====================================================

    public async Task<bool> DeleteAsync(int id)
    {
        var record =
            await _uow.Records.GetByIdAsync(id);

        if (record is null)
        {
            return false;
        }

        _uow.Records.Delete(record);

        await _uow.SaveChangesAsync();

        return true;
    }


    // =====================================================
    // Search
    // =====================================================

    public async Task<PagedResult<RecordDto>> SearchAsync(
        string? name,
        string? idNumber,
        int pageNumber,
        int pageSize)
    {
        if (pageNumber <= 0)
        {
            pageNumber = 1;
        }

        if (pageSize <= 0 ||
            pageSize > 100)
        {
            pageSize = 10;
        }

        var nameTerm =
            string.IsNullOrWhiteSpace(name)
                ? null
                : name.Trim();

        var idTerm =
            NormalizeDigits(
                string.IsNullOrWhiteSpace(idNumber)
                    ? null
                    : idNumber.Trim());

        var result =
            await _uow.Records.SearchAsync(
                nameTerm,
                idTerm,
                pageNumber,
                pageSize);

        return new PagedResult<RecordDto>
        {
            Items = result.Items
                .Select(ToDto)
                .ToList(),

            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = result.TotalCount
        };
    }
}