using Microsoft.EntityFrameworkCore;
using Ocr.Domain.Repositories;
using Ocr.Model.Entities;

namespace Ocr.Infrastructure.Persistence.Repositories;

public class RecordRepository : IRecordRepository
{
    private readonly AppDbContext _db;

    public RecordRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Record?> GetByIdAsync(
        int id,
        CancellationToken ct = default)
    {
        return await _db.Records
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task AddAsync(
        Record record,
        CancellationToken ct = default)
    {
        await _db.Records.AddAsync(record, ct);
    }

    public void Update(Record record)
    {
        _db.Records.Update(record);
    }

    public void Delete(Record record)
    {
        _db.Records.Remove(record);
    }

    public async Task<(IReadOnlyList<Record> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Records
            .AsNoTracking()
            .OrderBy(r => r.Id);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Record> Items, int TotalCount)> SearchAsync(
        string? name,
        string? idNumber,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        IQueryable<Record> query = _db.Records
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(name))
        {
            var nameTerm = name.Trim();

            query = query.Where(r =>
                r.Name != null &&
                EF.Functions.Like(r.Name, $"%{nameTerm}%"));
        }

        if (!string.IsNullOrWhiteSpace(idNumber))
        {
            var idTerm = idNumber.Trim();

            query = query.Where(r =>
                r.IdNumber != null &&
                (
                    r.IdNumber.Contains(idTerm) ||
                    EF.Functions.Like(
                        r.IdNumber,
                        $"%{idTerm}%") ||
                    r.IdNumber
                        .Replace(" ", "")
                        .Contains(idTerm)
                ));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}