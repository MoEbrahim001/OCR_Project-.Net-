using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using ClosedXML.Excel;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Ocr.Domain.Storage;
using Ocr.Core.Abstractions;
using Ocr.Domain.Services;
using Ocr.Domain.UnitOfWork;
using Ocr.Model.Entities;
using Ocr.ViewModel;

using ocr.viewmodel;
using Ocr.Domain.Models;
using DocumentFormat.OpenXml.Vml;

namespace Ocr.Core.Services;

public class RecordService : IRecordService
{
    private readonly IUnitOfWork _uow;
    private readonly IOcrClient _ocrClient;
    private readonly IOcrParser _parser;
    private readonly IImageStorage _imageStorage;

    public RecordService(
        IUnitOfWork uow,
        IOcrClient ocrClient,
        IOcrParser parser, IImageStorage imageStorage)
    {
        _uow = uow;
        _ocrClient = ocrClient;
        _parser = parser;
        _imageStorage = imageStorage;
    }


    // =====================================================
    // HELPERS
    // =====================================================

    private static DateOnly? ParseYmd(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value
            .Trim()
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace(" ", "")
            .Replace("--", "-");


        var digits =
            new string(
                value
                    .Where(c =>
                        char.IsDigit(c)
                        || c == '-')
                    .ToArray()
            );


        var parts =
            digits.Split(
                '-',
                StringSplitOptions.RemoveEmptyEntries
            );


        try
        {
            if (parts.Length == 3)
            {
                var year =
                    int.Parse(parts[0]);

                var month =
                    Math.Clamp(
                        int.Parse(parts[1]),
                        1,
                        12
                    );

                var day =
                    Math.Clamp(
                        int.Parse(parts[2]),
                        1,
                        28
                    );


                return new DateOnly(
                    year,
                    month,
                    day
                );
            }


            if (
                digits.Length == 8
                &&
                !digits.Contains('-')
            )
            {
                var year =
                    int.Parse(
                        digits.Substring(
                            0,
                            4
                        )
                    );

                var month =
                    int.Parse(
                        digits.Substring(
                            4,
                            2
                        )
                    );

                var day =
                    int.Parse(
                        digits.Substring(
                            6,
                            2
                        )
                    );


                return new DateOnly(
                    year,
                    month,
                    day
                );
            }
        }
        catch
        {
            return null;
        }


        return null;
    }


    private static RecordDto ToDto(
        Record record)
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


    // =====================================================
    // LIGHTWEIGHT RESPONSE AFTER CREATE / UPDATE
    //
    // Do NOT send Base64 images back to Angular.
    // =====================================================

    private static RecordDto ToSaveDto(
        Record record)
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

            null,
            null,

            record.Notes,
            record.CreatedAtUtc,

            null,
            null
        );
    }


    private static string? NormalizeDigits(
        string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }


        var sb =
            new StringBuilder(
                value.Length
            );


        foreach (var ch in value)
        {
            // Arabic-Indic digits
            if (
                ch >= '\u0660'
                &&
                ch <= '\u0669'
            )
            {
                sb.Append(
                    (char)(
                        '0'
                        +
                        (ch - '\u0660')
                    )
                );
            }

            // Extended Arabic-Indic digits
            else if (
                ch >= '\u06F0'
                &&
                ch <= '\u06F9'
            )
            {
                sb.Append(
                    (char)(
                        '0'
                        +
                        (ch - '\u06F0')
                    )
                );
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


    private static int? CalculateAge(
        DateOnly? dateOfBirth)
    {
        if (dateOfBirth is null)
        {
            return null;
        }


        var today =
            DateOnly.FromDateTime(
                DateTime.Today
            );


        var age =
            today.Year
            -
            dateOfBirth.Value.Year;


        if (
            today
            <
            dateOfBirth.Value
                .AddYears(age)
        )
        {
            age--;
        }


        return Math.Max(
            age,
            0
        );
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
            string.IsNullOrWhiteSpace(
                file.ContentType
            )
                ? "image/jpeg"
                : file.ContentType;


        return
            $"data:{contentType};base64,{base64}";
    }


    // =====================================================
    // OCR IMPORT
    // =====================================================

    public async Task<Record> ImportAsync(
        IFormFile frontImage,
        IFormFile backImage,
        int threshold,
        CancellationToken ct = default,
        CreateUpdateRecordDto? overrideDto = null)
    {
        // =================================================
        // FRONT OCR
        // =================================================

        using var frontStream =
            frontImage.OpenReadStream();


        var frontExtraction =
            await _ocrClient
                .ExtractFrontAsync(
                    frontStream,
                    frontImage.FileName,
                    frontImage.ContentType
                        ?? "application/octet-stream",
                    threshold,
                    ct
                );


        var frontSmart =
            _parser.ParseFront(
                frontExtraction
            );


        var frontRaw =
            JsonSerializer
                .Deserialize<FrontExtractDto>(
                    frontExtraction.RawJson
                );


        // =================================================
        // BACK OCR
        // =================================================

        using var backStream =
            backImage.OpenReadStream();


        var backExtraction =
            await _ocrClient
                .ExtractBackAsync(
                    backStream,
                    backImage.FileName,
                    backImage.ContentType
                        ?? "application/octet-stream",
                    threshold,
                    ct
                );


        var backSmart =
            _parser.ParseBack(
                backExtraction
            );


        // =================================================
        // IMAGES
        // =================================================

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


        // =================================================
        // RECORD
        // =================================================

        var record =
            new Record
            {
                Name =
                    frontSmart.Name
                    ?? frontRaw?.name,

                IdNumber =
                    NormalizeDigits(
                        frontSmart.NationalId
                        ?? frontRaw?.ID
                    ),

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


        await _uow.Records
            .AddAsync(
                record,
                ct
            );


        await _uow
            .SaveChangesAsync(ct);


        return record;
    }


    // =====================================================
    // GET BY ID
    // =====================================================

    public async Task<RecordDto?> GetAsync(
        int id)
    {
        var record =
            await _uow.Records
                .GetByIdAsync(id);


        return record is null
            ? null
            : ToDto(record);
    }


    // =====================================================
    // PAGINATION
    // =====================================================

    public async Task<PagedResult<RecordDto>>
        GetPagedAsync(
            int pageNumber,
            int pageSize)
    {
        if (pageNumber <= 0)
        {
            pageNumber = 1;
        }


        if (
            pageSize <= 0
            ||
            pageSize > 100
        )
        {
            pageSize = 10;
        }


        var result =
            await _uow.Records
                .GetPagedAsync(
                    pageNumber,
                    pageSize
                );


        return new PagedResult<RecordDto>
        {
            Items =
                result.Items
                    .Select(ToDto)
                    .ToList(),

            PageNumber =
                pageNumber,

            PageSize =
                pageSize,

            TotalCount =
                result.TotalCount
        };
    }


    // =====================================================
    // CREATE
    // =====================================================

    public async Task<RecordDto> CreateAsync(
     CreateUpdateRecordDto dto)
    {
        string? frontImageKey = null;
        string? backImageKey = null;

        try
        {
         
            frontImageKey =
        await SaveDataUrlAsync(
            dto.FrontImageDataUrl,
            ImageSide.Front
        );

            backImageKey =
                await SaveDataUrlAsync(
                    dto.BackImageDataUrl,
                    ImageSide.Back
                );


            // ==========================================
            // RECORD
            // ==========================================

            var record =
                new Record
                {
                    Name =
                        dto.Name,

                    IdNumber =
                        NormalizeDigits(
                            dto.IdNumber
                        ),

                    DateOfBirth =
                        dto.DateOfBirth,

                    Address =
                        dto.Address,

                    Gender =
                        dto.Gender,

                    Profession =
                        dto.Profession,

                    MaritalStatus =
                        dto.MaritalStatus,

                    Religion =
                        dto.Religion,

                    EndDate =
                        dto.EndDate,

                    PhotoBase64 =
                        dto.PhotoBase64,

                    FaceBase64 =
                        dto.FaceBase64,

                    Notes =
                        dto.Notes,

                    // IMPORTANT:
                    // These now contain STORAGE KEYS,
                    // not Base64 Data URLs.

                    FrontImageDataUrl =
                        frontImageKey,

                    BackImageDataUrl =
                        backImageKey
                };


            await _uow.Records
                .AddAsync(record);


            await _uow
                .SaveChangesAsync();


            return ToSaveDto(
                record
            );
        }
        catch
        {
            // If DB/save fails,
            // don't leave orphan image files.

            if (
                !string.IsNullOrWhiteSpace(
                    frontImageKey
                )
                &&
                !frontImageKey.StartsWith(
                    "data:image/",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                await _imageStorage.DeleteAsync(
                    frontImageKey
                );
            }


            if (
                !string.IsNullOrWhiteSpace(
                    backImageKey
                )
                &&
                !backImageKey.StartsWith(
                    "data:image/",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                await _imageStorage.DeleteAsync(
                    backImageKey
                );
            }


            throw;
        }
    }


    // =====================================================
    // UPDATE
    //
    // Uses a lightweight stub.
    // No SELECT of the Base64 images before UPDATE.
    // =====================================================

    public async Task<RecordDto?> UpdateAsync(
        int id,
        CreateUpdateRecordDto dto)
    {
        var record =
            new Record
            {
                Id =
                    id,

                Name =
                    dto.Name,

                IdNumber =
                    NormalizeDigits(
                        dto.IdNumber
                    ),

                DateOfBirth =
                    dto.DateOfBirth,

                Address =
                    dto.Address,

                Gender =
                    dto.Gender,

                Profession =
                    dto.Profession,

                MaritalStatus =
                    dto.MaritalStatus,

                Religion =
                    dto.Religion,

                EndDate =
                    dto.EndDate,

                Notes =
                    dto.Notes,

                // Null means unchanged.
                PhotoBase64 =
                    dto.PhotoBase64,

                FaceBase64 =
                    dto.FaceBase64,

                FrontImageDataUrl =
                    dto.FrontImageDataUrl,

                BackImageDataUrl =
                    dto.BackImageDataUrl
            };


        try
        {
            _uow.Records
                .Update(record);


            await _uow
                .SaveChangesAsync();


            // Do NOT send images back.
            return ToSaveDto(
                record
            );
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }


    // =====================================================
    // DELETE
    //
    // Stub delete:
    // no SELECT before DELETE.
    // =====================================================

    public async Task<bool> DeleteAsync(
        int id)
    {
        var record =
            new Record
            {
                Id = id
            };


        try
        {
            _uow.Records
                .Delete(record);


            var affected =
                await _uow
                    .SaveChangesAsync();


            return affected > 0;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }


    // =====================================================
    // SEARCH
    // =====================================================

    public async Task<PagedResult<RecordDto>>
     SearchAsync(
         RecordSearchCriteria criteria)
    {
        if (criteria.PageNumber <= 0)
        {
            criteria.PageNumber = 1;
        }


        if (
            criteria.PageSize <= 0
            ||
            criteria.PageSize > 100
        )
        {
            criteria.PageSize = 10;
        }


        criteria.Name =
            CleanSearchText(
                criteria.Name
            );


        criteria.IdNumber =
            NormalizeDigits(
                CleanSearchText(
                    criteria.IdNumber
                )
            );


        criteria.Address =
            CleanSearchText(
                criteria.Address
            );


        criteria.Gender =
            CleanSearchText(
                criteria.Gender
            );


        criteria.Religion =
            CleanSearchText(
                criteria.Religion
            );


        criteria.Profession =
            CleanSearchText(
                criteria.Profession
            );


        criteria.MaritalStatus =
            CleanSearchText(
                criteria.MaritalStatus
            );


        criteria.HusbandName =
            CleanSearchText(
                criteria.HusbandName
            );


        var result =
            await _uow.Records
                .SearchAsync(
                    criteria
                );


        return new PagedResult<RecordDto>
        {
            Items =
                result.Items
                    .Select(ToDto)
                    .ToList(),

            PageNumber =
                criteria.PageNumber,

            PageSize =
                criteria.PageSize,

            TotalCount =
                result.TotalCount
        };
    }


    private static string? CleanSearchText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }


    // =====================================================
    // EXPORT EXCEL
    // =====================================================

    public async Task<byte[]> ExportExcelAsync(
      string? name,
      string? idNumber,
      CancellationToken ct = default)
    {
        var normalizedId =
            NormalizeDigits(idNumber);

        var records =
            await _uow.Records
                .GetForExportAsync(
                    string.IsNullOrWhiteSpace(name)
                        ? null
                        : name.Trim(),

                    string.IsNullOrWhiteSpace(normalizedId)
                        ? null
                        : normalizedId,

                    ct
                );

        return GenerateExcelFile(records);
    }
    public async Task<byte[]> ExportExcelByIdsAsync(
    IReadOnlyCollection<int> ids,
    CancellationToken ct = default)
    {
        if (
            ids == null
            ||
            ids.Count == 0
        )
        {
            return GenerateExcelFile(
                Array.Empty<Record>()
            );
        }

        var records =
            await _uow.Records
                .GetForExportByIdsAsync(
                    ids,
                    ct
                );

        return GenerateExcelFile(records);
    }
    private static byte[] GenerateExcelFile(
    IReadOnlyList<Record> records)
    {
        using var workbook =
            new XLWorkbook();

        var worksheet =
            workbook.Worksheets
                .Add("National ID Records");

        var headers =
            new[]
            {
            "#",
            "Name",
            "ID Number",
            "Address",
            "Date of Birth",
            "Age",
            "Religion",
            "Gender",
            "Occupation",
            "Marital Status",
            "Expiry Date"
            };

        for (
            var column = 0;
            column < headers.Length;
            column++
        )
        {
            var cell =
                worksheet.Cell(
                    1,
                    column + 1
                );

            cell.Value =
                headers[column];

            cell.Style.Font.Bold =
                true;

            cell.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            cell.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;
        }

        var row = 2;

        foreach (
            var record in records
        )
        {
            worksheet
                .Cell(row, 1)
                .Value =
                row - 1;

            worksheet
                .Cell(row, 2)
                .Value =
                record.Name ?? "";

            worksheet
                .Cell(row, 3)
                .Value =
                NormalizeDigits(
                    record.IdNumber
                ) ?? "";

            worksheet
                .Cell(row, 4)
                .Value =
                record.Address ?? "";

            worksheet
                .Cell(row, 5)
                .Value =
                record.DateOfBirth
                    ?.ToString("yyyy-MM-dd")
                ?? "";

            worksheet
                .Cell(row, 6)
                .Value =
                CalculateAge(
                    record.DateOfBirth
                ) ?? 0;

            worksheet
                .Cell(row, 7)
                .Value =
                record.Religion ?? "";

            worksheet
                .Cell(row, 8)
                .Value =
                record.Gender ?? "";

            worksheet
                .Cell(row, 9)
                .Value =
                record.Profession ?? "";

            worksheet
                .Cell(row, 10)
                .Value =
                record.MaritalStatus ?? "";

            worksheet
                .Cell(row, 11)
                .Value =
                record.EndDate
                    ?.ToString("yyyy-MM-dd")
                ?? "";

            row++;
        }

        var usedRange =
            worksheet.RangeUsed();

        if (
            usedRange is not null
        )
        {
            usedRange
                .Style
                .Alignment
                .Vertical =
                XLAlignmentVerticalValues.Center;

            usedRange
                .Style
                .Border
                .BottomBorder =
                XLBorderStyleValues.Thin;

            usedRange
                .Style
                .Border
                .BottomBorderColor =
                XLColor.LightGray;
        }

        worksheet
            .Columns()
            .AdjustToContents();

        foreach (
            var column
            in worksheet.ColumnsUsed()
        )
        {
            if (
                column.Width > 45
            )
            {
                column.Width = 45;
            }
        }

        worksheet
            .Row(1)
            .Height = 24;

        worksheet
            .Column(3)
            .Style
            .NumberFormat
            .Format = "@";

        using var stream =
            new MemoryStream();

        workbook.SaveAs(stream);

        return stream.ToArray();
    }


    // =====================================================
    // EXPORT PDF
    // =====================================================

    public async Task<byte[]> ExportPdfAsync(
    string? name,
    string? idNumber,
    CancellationToken ct = default)
    {
        var normalizedId =
            NormalizeDigits(idNumber);

        var records =
            await _uow.Records
                .GetForExportAsync(
                    string.IsNullOrWhiteSpace(name)
                        ? null
                        : name.Trim(),

                    string.IsNullOrWhiteSpace(normalizedId)
                        ? null
                        : normalizedId,

                    ct
                );

        return GeneratePdfFile(records);
    }
    public async Task<byte[]> ExportPdfByIdsAsync(
    IReadOnlyCollection<int> ids,
    CancellationToken ct = default)
    {
        if (
            ids == null
            ||
            ids.Count == 0
        )
        {
            return GeneratePdfFile(
                Array.Empty<Record>()
            );
        }

        var records =
            await _uow.Records
                .GetForExportByIdsAsync(
                    ids,
                    ct
                );

        return GeneratePdfFile(records);
    }
    private static byte[] GeneratePdfFile(
        IReadOnlyList<Record> records)
    {
        var generatedAt =
            DateTime.Now;


        var document =
            QuestPDF.Fluent.Document
                .Create(container =>
                {
                    container.Page(page =>
                    {
                        // =====================================
                        // PAGE
                        // =====================================

                        page.Size(
                            PageSizes.A4
                                .Landscape()
                        );


                        page.Margin(20);


                        page.DefaultTextStyle(
                            style =>
                                style
                                    .FontFamily(
                                        "Arial"
                                    )
                                    .FontSize(7.5f)
                        );


                        // Keep table column order LTR.
                        page.ContentFromLeftToRight();


                        // =====================================
                        // HEADER
                        // =====================================

                        page.Header()
                            .Column(column =>
                            {
                                column.Spacing(3);


                                column.Item()
                                    .Text(
                                        "National ID Registration - Records Export"
                                    )
                                    .FontSize(15)
                                    .Bold()
                                    .FontColor(
                                        Colors.Blue.Darken2
                                    );


                                column.Item()
                                    .Text(
                                        $"Generated: {generatedAt:yyyy-MM-dd HH:mm}"
                                    )
                                    .FontSize(7)
                                    .FontColor(
                                        Colors.Grey.Darken1
                                    );


                                column.Item()
                                    .Text(
                                        $"Total Records: {records.Count}"
                                    )
                                    .FontSize(7)
                                    .FontColor(
                                        Colors.Grey.Darken1
                                    );
                            });


                        // =====================================
                        // CONTENT
                        // =====================================

                        page.Content()
                            .PaddingVertical(8)
                            .Table(table =>
                            {
                                // =================================
                                // COLUMN WIDTHS
                                // =================================

                                table.ColumnsDefinition(
                                    columns =>
                                    {
                                        columns.ConstantColumn(24);     // #
                                        columns.RelativeColumn(1.5f);  // Name
                                        columns.RelativeColumn(1.55f); // ID
                                        columns.RelativeColumn(2.3f);  // Address
                                        columns.RelativeColumn(1.1f);  // DOB
                                        columns.ConstantColumn(34);    // Age
                                        columns.RelativeColumn(0.9f);  // Religion
                                        columns.RelativeColumn(0.85f); // Gender
                                        columns.RelativeColumn(1.9f);  // Occupation
                                        columns.RelativeColumn(1.1f);  // Marital
                                        columns.RelativeColumn(1.1f);  // Expiry
                                    }
                                );


                                // =================================
                                // CELL STYLES
                                // =================================

                                static IContainer HeaderCellStyle(
                                    IContainer container)
                                {
                                    return container
                                        .Background(
                                            Colors.Blue.Darken2
                                        )
                                        .Border(
                                            0.5f
                                        )
                                        .BorderColor(
                                            Colors.Grey.Lighten1
                                        )
                                        .PaddingVertical(
                                            5
                                        )
                                        .PaddingHorizontal(
                                            3
                                        )
                                        .AlignCenter()
                                        .AlignMiddle();
                                }


                                static IContainer BodyCellStyle(
                                    IContainer container)
                                {
                                    return container
                                        .BorderBottom(
                                            0.5f
                                        )
                                        .BorderColor(
                                            Colors.Grey.Lighten2
                                        )
                                        .PaddingVertical(
                                            4
                                        )
                                        .PaddingHorizontal(
                                            3
                                        )
                                        .AlignMiddle();
                                }


                                // =================================
                                // TABLE HEADER
                                // =================================

                                table.Header(header =>
                                {
                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("#")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("Name")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("ID Number")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("Address")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("Date of Birth")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("Age")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("Religion")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("Gender")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("Occupation")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("Marital Status")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);

                                    header.Cell()
                                        .Element(HeaderCellStyle)
                                        .Text("Expiry Date")
                                        .FontSize(6.5f)
                                        .Bold()
                                        .FontColor(Colors.White);
                                });


                                // =================================
                                // TABLE DATA
                                // =================================

                                var index =
                                    1;


                                foreach (
                                    var record
                                    in records
                                )
                                {
                                    // #
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .AlignCenter()
                                        .ContentFromLeftToRight()
                                        .Text(index.ToString());


                                    // NAME
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .ContentFromRightToLeft()
                                        .AlignRight()
                                        .Text(
                                            record.Name
                                            ?? ""
                                        );


                                    // ID
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .ContentFromLeftToRight()
                                        .AlignCenter()
                                        .Text(
                                            NormalizeDigits(
                                                record.IdNumber
                                            )
                                            ?? ""
                                        );


                                    // ADDRESS
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .ContentFromRightToLeft()
                                        .AlignRight()
                                        .Text(
                                            record.Address
                                            ?? ""
                                        );


                                    // DOB
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .ContentFromLeftToRight()
                                        .AlignCenter()
                                        .Text(
                                            record.DateOfBirth
                                                ?.ToString(
                                                    "yyyy-MM-dd"
                                                )
                                            ?? ""
                                        );


                                    // AGE
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .ContentFromLeftToRight()
                                        .AlignCenter()
                                        .Text(
                                            CalculateAge(
                                                record.DateOfBirth
                                            )
                                                ?.ToString()
                                            ?? ""
                                        );


                                    // RELIGION
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .ContentFromRightToLeft()
                                        .AlignRight()
                                        .Text(
                                            record.Religion
                                            ?? ""
                                        );


                                    // GENDER
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .ContentFromRightToLeft()
                                        .AlignRight()
                                        .Text(
                                            record.Gender
                                            ?? ""
                                        );


                                    // OCCUPATION
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .ContentFromRightToLeft()
                                        .AlignRight()
                                        .Text(
                                            record.Profession
                                            ?? ""
                                        );


                                    // MARITAL STATUS
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .ContentFromRightToLeft()
                                        .AlignRight()
                                        .Text(
                                            record.MaritalStatus
                                            ?? ""
                                        );


                                    // EXPIRY
                                    table.Cell()
                                        .Element(BodyCellStyle)
                                        .ContentFromLeftToRight()
                                        .AlignCenter()
                                        .Text(
                                            record.EndDate
                                                ?.ToString(
                                                    "yyyy-MM-dd"
                                                )
                                            ?? ""
                                        );


                                    index++;
                                }
                            });


                        // =====================================
                        // FOOTER
                        // =====================================

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text
                                    .DefaultTextStyle(
                                        style =>
                                            style
                                                .FontSize(7)
                                                .FontColor(
                                                    Colors.Grey.Darken1
                                                )
                                    );


                                text.Span(
                                    "Page "
                                );


                                text.CurrentPageNumber();


                                text.Span(
                                    " of "
                                );


                                text.TotalPages();
                            });
                    });
                });


        return document
            .GeneratePdf();
    }
    private async Task<string?> SaveDataUrlAsync(
       string? dataUrl,
       ImageSide side,
       CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            return null;
        }

        if (
            !dataUrl.StartsWith(
                "data:image/",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return dataUrl;
        }

        var commaIndex =
            dataUrl.IndexOf(',');

        if (commaIndex <= 0)
        {
            throw new InvalidOperationException(
                "Invalid image Data URL."
            );
        }

        var header =
            dataUrl.Substring(
                0,
                commaIndex
            );

        var base64 =
            dataUrl.Substring(
                commaIndex + 1
            );

        string contentType;

        if (
            header.StartsWith(
                "data:image/jpeg",
                StringComparison.OrdinalIgnoreCase
            )
            ||
            header.StartsWith(
                "data:image/jpg",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            contentType = "image/jpeg";
        }
        else if (
            header.StartsWith(
                "data:image/png",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            contentType = "image/png";
        }
        else
        {
            throw new InvalidOperationException(
                "Only JPEG and PNG images are supported."
            );
        }

        byte[] bytes;

        try
        {
            bytes =
                Convert.FromBase64String(
                    base64
                );
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Invalid Base64 image data."
            );
        }

        return await _imageStorage.SaveAsync(
            bytes,
            contentType,
            side,
            ct
        );
    }
}