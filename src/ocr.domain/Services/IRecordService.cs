using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Ocr.Domain.Models;
using Ocr.Model.Entities;
using Ocr.ViewModel;

namespace Ocr.Domain.Services;

public interface IRecordService
{
    Task<RecordDto?> GetAsync(
        int id);

    Task<PagedResult<RecordDto>> GetPagedAsync(
        int pageNumber,
        int pageSize);

    Task<RecordDto> CreateAsync(
        CreateUpdateRecordDto dto);

    Task<RecordDto?> UpdateAsync(
        int id,
        CreateUpdateRecordDto dto);

    Task<bool> DeleteAsync(
        int id);

    Task<Record> ImportAsync(
        IFormFile frontImage,
        IFormFile backImage,
        int threshold,
        CancellationToken ct = default,
        CreateUpdateRecordDto? overrideDto = null);

    Task<PagedResult<RecordDto>> SearchAsync(
       RecordSearchCriteria criteria);

    // Existing export by search filters
    Task<byte[]> ExportExcelAsync(
        string? name,
        string? idNumber,
        CancellationToken ct = default);

    Task<byte[]> ExportPdfAsync(
        string? name,
        string? idNumber,
        CancellationToken ct = default);

    // New export for checked rows only
    Task<byte[]> ExportExcelByIdsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken ct = default);

    Task<byte[]> ExportPdfByIdsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken ct = default);
}
