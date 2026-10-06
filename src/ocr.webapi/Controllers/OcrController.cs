using Microsoft.AspNetCore.Mvc;

using Ocr.Core.Abstractions;
using Ocr.Core.OCR.DTOs;
using Ocr.Domain.Services;
using Ocr.ViewModel;
using Ocr.WebApi.Requests;

namespace Ocr.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OcrController : ControllerBase
{
    private readonly IOcrClient _ocr;
    private readonly IOcrParser _parser;
    private readonly IConfiguration _config;
    private readonly IRecordService _records;

    public OcrController(
        IOcrClient ocr,
        IOcrParser parser,
        IConfiguration config,
        IRecordService records)
    {
        _ocr = ocr;
        _parser = parser;
        _config = config;
        _records = records;
    }


    // =====================================================
    // FRONT OCR
    // POST: /api/Ocr/extract/front
    // =====================================================

    [HttpPost("extract/front")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(
        typeof(FrontOcrResult),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FrontOcrResult>> ExtractFront(
        IFormFile file,
        [FromQuery] int? threshold,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        var t =
            threshold
            ?? _config.GetValue<int?>(
                "Ocr:DefaultThreshold")
            ?? 75;

        await using var stream =
            file.OpenReadStream();

        var extraction =
            await _ocr.ExtractFrontAsync(
                stream,
                file.FileName,
                file.ContentType
                    ?? "application/octet-stream",
                t,
                ct);

        var result =
            _parser.ParseFront(extraction);

        return Ok(result);
    }


    // =====================================================
    // BACK OCR
    // POST: /api/Ocr/extract/back
    // =====================================================

    [HttpPost("extract/back")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(
        typeof(BackOcrResult),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BackOcrResult>> ExtractBack(
        IFormFile file,
        [FromQuery] int? threshold,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        var t =
            threshold
            ?? _config.GetValue<int?>(
                "Ocr:DefaultThreshold")
            ?? 75;

        await using var stream =
            file.OpenReadStream();

        var extraction =
            await _ocr.ExtractBackAsync(
                stream,
                file.FileName,
                file.ContentType
                    ?? "application/octet-stream",
                t,
                ct);

        var result =
            _parser.ParseBack(extraction);

        return Ok(result);
    }


    // =====================================================
    // IMPORT FRONT + BACK + SAVE RECORD
    // POST: /api/Ocr/import
    // =====================================================

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(
        typeof(RecordDto),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecordDto>> Import(
        [FromForm] OcrFileRequest request,
        CancellationToken ct = default)
    {
        if (request.FrontImage is null ||
            request.FrontImage.Length == 0)
        {
            return BadRequest(
                "frontImage is required");
        }

        if (request.BackImage is null ||
            request.BackImage.Length == 0)
        {
            return BadRequest(
                "backImage is required");
        }

        var threshold =
            request.Threshold
            ?? _config.GetValue<int?>(
                "Ocr:DefaultThreshold")
            ?? 120;

        var record =
            await _records.ImportAsync(
                request.FrontImage,
                request.BackImage,
                threshold,
                ct);

        var dto =
            await _records.GetAsync(
                record.Id);

        if (dto is null)
        {
            return Problem(
                "Record was created but could not be retrieved.");
        }

        return Created(
            $"/api/Records/{record.Id}",
            dto);
    }
}