using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Ocr.Domain.Models;
using Ocr.Domain.Services;
using Ocr.ViewModel;
using Ocr.WebApi.Requests;

namespace Ocr.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecordsController : ControllerBase
{
    private readonly IRecordService _svc;

    public RecordsController(
        IRecordService svc)
    {
        _svc = svc;
    }


    // =====================================================
    // HELPERS
    // =====================================================

    private static DateOnly? ParseYmd(
        string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        return DateOnly.TryParseExact(
            s.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var d
        )
            ? d
            : null;
    }


    // =====================================================
    // LIST
    // =====================================================

    [HttpGet]
    public async Task<ActionResult<PagedResult<RecordDto>>> Get(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var result =
            await _svc.GetPagedAsync(
                pageNumber,
                pageSize
            );

        return Ok(result);
    }


    // =====================================================
    // GET ONE
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecordDto>> GetOne(
        int id)
    {
        var record =
            await _svc.GetAsync(id);

        return record is null
            ? NotFound()
            : Ok(record);
    }


    // =====================================================
    // CREATE
    // =====================================================

    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<RecordDto>> Create(
        [FromBody] CreateUpdateRecordDto dto)
    {
        var created =
            await _svc.CreateAsync(dto);

        return CreatedAtAction(
            nameof(GetOne),
            new
            {
                id = created.Id
            },
            created
        );
    }


    // =====================================================
    // UPDATE
    // =====================================================

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecordDto>> Update(
        int id,
        [FromBody] CreateUpdateRecordDto dto)
    {
        var updated =
            await _svc.UpdateAsync(
                id,
                dto
            );

        return updated is null
            ? NotFound()
            : Ok(updated);
    }


    // =====================================================
    // DELETE
    // =====================================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id)
    {
        var ok =
            await _svc.DeleteAsync(id);

        return ok
            ? NoContent()
            : NotFound();
    }


    // =====================================================
    // SEARCH
    // =====================================================
    [HttpGet("Search")]
    public async Task<ActionResult<PagedResult<RecordDto>>> Search(
        [FromQuery] RecordSearchCriteria criteria)
    {
        var result =
            await _svc.SearchAsync(
                criteria
            );

        return Ok(result);
    }


    // =====================================================
    // EXPORT ALL / SEARCH RESULTS - EXCEL
    // =====================================================

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] string? name,
        [FromQuery] string? idNumber,
        CancellationToken ct)
    {
        var file =
            await _svc.ExportExcelAsync(
                name,
                idNumber,
                ct
            );

        var fileName =
            $"National-ID-Records-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";

        return File(
            file,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }


    // =====================================================
    // EXPORT ALL / SEARCH RESULTS - PDF
    // =====================================================

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] string? name,
        [FromQuery] string? idNumber,
        CancellationToken ct)
    {
        var file =
            await _svc.ExportPdfAsync(
                name,
                idNumber,
                ct
            );

        var fileName =
            $"National-ID-Records-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";

        return File(
            file,
            "application/pdf",
            fileName
        );
    }


    // =====================================================
    // EXPORT CHECKED ROWS ONLY - EXCEL
    // =====================================================

    [HttpPost("export/excel/selected")]
    public async Task<IActionResult> ExportSelectedExcel(
        [FromBody] ExportSelectedRecordsRequest request,
        CancellationToken ct)
    {
        var ids =
            request?.RecordIds?
                .Where(id => id > 0)
                .Distinct()
                .ToArray()
            ?? Array.Empty<int>();

        if (ids.Length == 0)
        {
            return BadRequest(
                new
                {
                    message =
                        "Select at least one record."
                }
            );
        }

        var file =
            await _svc.ExportExcelByIdsAsync(
                ids,
                ct
            );

        var fileName =
            $"National-ID-Selected-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";

        return File(
            file,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }


    // =====================================================
    // EXPORT CHECKED ROWS ONLY - PDF
    // =====================================================

    [HttpPost("export/pdf/selected")]
    public async Task<IActionResult> ExportSelectedPdf(
        [FromBody] ExportSelectedRecordsRequest request,
        CancellationToken ct)
    {
        var ids =
            request?.RecordIds?
                .Where(id => id > 0)
                .Distinct()
                .ToArray()
            ?? Array.Empty<int>();

        if (ids.Length == 0)
        {
            return BadRequest(
                new
                {
                    message =
                        "Select at least one record."
                }
            );
        }

        var file =
            await _svc.ExportPdfByIdsAsync(
                ids,
                ct
            );

        var fileName =
            $"National-ID-Selected-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";

        return File(
            file,
            "application/pdf",
            fileName
        );
    }
}
