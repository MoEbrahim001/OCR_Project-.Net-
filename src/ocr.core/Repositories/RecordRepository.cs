using Microsoft.EntityFrameworkCore;
using Ocr.Domain.Repositories;
using Ocr.Model;
using Ocr.Model.Entities;
using System.Linq.Expressions;

namespace Ocr.Core.Repositories;

public class RecordRepository : IRecordRepository
{
    private readonly AppDbContext _db;
    public RecordRepository(AppDbContext db) { _db = db; }


    public Task AddAsync(Record record, CancellationToken ct = default)
        => _db.Set<Record>().AddAsync(record, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
    public async Task DeleteAsync(Record entity) { _db.Records.Remove(entity); await Task.CompletedTask; }
    public async Task<Record?> GetByIdAsync(int id) => await _db.Records.FindAsync(id);
    public IQueryable<Record> Query(Expression<Func<Record, bool>>? filter = null)
    {
        var q = _db.Records.AsQueryable();
        if (filter != null) q = q.Where(filter);
        return q.OrderByDescending(r => r.CreatedAtUtc);
    }
    public async Task UpdateAsync(Record entity) { _db.Records.Update(entity); await Task.CompletedTask; }
}
