using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

using Ocr.ViewModel;
using Ocr.Domain.UnitOfWork;
using Ocr.Model.Entities;
using Ocr.WebApi.Requests;
using Ocr.Domain.Dtos;
using Ocr.Domain.Services;

namespace Ocr.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OcrController : ControllerBase
{
    private readonly IOcrClient _ocr;
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly IOcrParser _parser;

    public OcrController(IOcrClient ocr, IUnitOfWork uow, IOcrParser parser, IConfiguration config)
    {
        _ocr = ocr;
        _uow = uow;
        _parser = parser;
        _config = config;
    }

    // ---------- helpers ----------
    private static DateOnly? ParseDate(string? s) => DateOnly.TryParse(s, out var d) ? d : null;
    private static string? Get(JsonElement root, string name) => root.TryGetProperty(name, out var p) ? p.GetString() : null;

    private static RecordDto ToDto(Record e) => new(
        e.Id,
        e.Name,
        e.IdNumber,
        e.DateOfBirth,
        e.Address,
        e.Gender,
        e.Profession,
        e.MaritalStatus,
        e.Religion,
        e.EndDate,
        e.PhotoBase64,
        e.FaceBase64,
        e.Notes,
        e.CreatedAtUtc
    );

    // ---------- NEW: GET (paged list) ----------
    // GET /api/Ocr?pageNumber=1&pageSize=10&name=&idNumber=&dateOfBirth=&address=
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RecordDto>>> Get(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? name = null,
        [FromQuery] string? idNumber = null,
        [FromQuery] DateOnly? dateOfBirth = null,
        [FromQuery] string? address = null,
        CancellationToken ct = default)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 10;

        var q = _uow.Records.Query();

        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(r => r.Name != null && r.Name.ToLower().Contains(name.ToLower()));

        if (!string.IsNullOrWhiteSpace(idNumber))
            q = q.Where(r => r.IdNumber != null && r.IdNumber.Contains(idNumber));

        if (dateOfBirth.HasValue)
            q = q.Where(r => r.DateOfBirth == dateOfBirth);

        if (!string.IsNullOrWhiteSpace(address))
            q = q.Where(r => r.Address != null && r.Address.ToLower().Contains(address.ToLower()));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(r => r.CreatedAtUtc) // optional ordering
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = new PagedResult<RecordDto>
        {
            Items = items.Select(ToDto).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total
        };

        return Ok(result);
    }

    // ---------- NEW: GET one ----------
    // GET /api/Ocr/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecordDto>> GetOne([FromRoute] int id, CancellationToken ct = default)
    {
        var e = await _uow.Records.Query().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (e is null) return NotFound();
        return Ok(ToDto(e));
    }

    // ---------- OCR Extract: Front ----------
    [HttpPost("extract/front")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FrontOcrResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<FrontOcrResult>> ExtractFront(
        IFormFile file,
        [FromQuery] int? threshold,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        var t = threshold ?? _config.GetValue<int?>("Ocr:DefaultThreshold") ?? 75;

        await using var s = file.OpenReadStream();
        var extraction = await _ocr.ExtractFrontAsync(s, file.FileName, file.ContentType, t, ct);
        var result = _parser.ParseFront(extraction);

        return Ok(result);
    }

    // ---------- OCR Extract: Back ----------
    [HttpPost("extract/back")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BackOcrResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackOcrResult>> ExtractBack(
        IFormFile file,
        [FromQuery] int? threshold,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        var t = threshold ?? _config.GetValue<int?>("Ocr:DefaultThreshold") ?? 75;

        await using var s = file.OpenReadStream();
        var extraction = await _ocr.ExtractBackAsync(s, file.FileName, file.ContentType, t, ct);
        var result = _parser.ParseBack(extraction);

        return Ok(result);
    }

    // ---------- Import (already existed) ----------
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RecordDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<RecordDto>> Import([FromForm] OcrFileRequest request)
    {
        if (request.FrontImage is null || request.FrontImage.Length == 0)
            return BadRequest("frontImage is required");

        // Front
        using var frontStream = request.FrontImage.OpenReadStream();
        var frontRaw = await _ocr.ExtractFrontAsync(frontStream, request.FrontImage.FileName, request.FrontImage.ContentType, request.Threshold??120);
        var front = JsonDocument.Parse(frontRaw.RawJson).RootElement;

        var entity = new Record
        {
            Name = Get(front, "name"),
            Address = Get(front, "address"),
            IdNumber = Get(front, "ID"),
            DateOfBirth = ParseDate(Get(front, "DOB")),
            PhotoBase64 = Get(front, "image"),
            FaceBase64 = Get(front, "face")
        };

        // Back (optional)
        if (request.BackImage is not null && request.BackImage.Length > 0)
        {
            using var backStream = request.BackImage.OpenReadStream();
            var backRaw = await _ocr.ExtractBackAsync(backStream, request.BackImage.FileName, request.BackImage.ContentType, request.Threshold ?? 120);
            var back = JsonDocument.Parse(backRaw.RawJson).RootElement;

            entity.Profession = Get(back, "profession");
            entity.Religion = Get(back, "religion");
            entity.Gender = Get(back, "gender");
            entity.MaritalStatus = Get(back, "marital_status");
            entity.EndDate = ParseDate(Get(back, "enddate"));
            
        }

        await _uow.Records.AddAsync(entity);
        await _uow.SaveChangesAsync();

        var dto = ToDto(entity);

        // Location header points to /api/Ocr/{id} now that we have GetOne here
        return CreatedAtAction(nameof(GetOne), new { id = entity.Id }, dto);
    }
}
