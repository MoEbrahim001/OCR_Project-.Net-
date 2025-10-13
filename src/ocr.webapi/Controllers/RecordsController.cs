using Microsoft.AspNetCore.Mvc;
using Ocr.Domain.Services;
using Ocr.Model.Entities;
using Ocr.ViewModel;
using Ocr.WebApi.Requests;
using System.Globalization;

namespace Ocr.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecordsController : ControllerBase
{
    private readonly IRecordService _svc;

    public RecordsController(IRecordService svc) { _svc = svc; }
    private static DateOnly? ParseYmd(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        // ????? yyyy-MM-dd
        return DateOnly.TryParseExact(
            s.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var d
        ) ? d : null;
    }
    [HttpGet]
    public async Task<ActionResult<PagedResult<RecordDto>>> Get(
     [FromQuery] int pageNumber = 1,
     [FromQuery] int pageSize = 10)
    {
        var result = await _svc.GetPagedAsync(pageNumber, pageSize);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecordDto>> GetOne(int id)
    {
        var r = await _svc.GetAsync(id);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<RecordDto>> Create([FromBody] CreateUpdateRecordDto dto)
    {
        var created = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetOne), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecordDto>> Update(int id, [FromBody] CreateUpdateRecordDto dto)
    {
        var updated = await _svc.UpdateAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _svc.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }
    [HttpGet("Search")]
    public async Task<ActionResult<PagedResult<RecordDto>>> Search(
      [FromQuery] string? name,
      [FromQuery] string? idNumber,
      [FromQuery] int pageNumber = 1,
      [FromQuery] int pageSize = 10)
    {
        var result = await _svc.SearchAsync(name, idNumber, pageNumber, pageSize);
        return Ok(result);
    }


}
