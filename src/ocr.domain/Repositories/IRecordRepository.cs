using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ocr.Model.Entities;

namespace Ocr.Domain.Repositories;

public interface IRecordRepository
{
    Task<Record?> GetByIdAsync(
        int id,
        CancellationToken ct = default);

    Task AddAsync(
        Record record,
        CancellationToken ct = default);

    void Update(Record record);

    void Delete(Record record);

    Task<(IReadOnlyList<Record> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);

    Task<(IReadOnlyList<Record> Items, int TotalCount)> SearchAsync(
        string? name,
        string? idNumber,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<Record>> GetForExportAsync(
        string? name,
        string? idNumber,
        CancellationToken ct = default);

    Task<IReadOnlyList<Record>> GetForExportByIdsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken ct = default);
}
