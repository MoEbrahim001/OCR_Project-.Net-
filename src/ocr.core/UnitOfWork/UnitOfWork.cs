using Ocr.Domain.Repositories;
using Ocr.Domain.UnitOfWork;
using Ocr.Model;
using Ocr.Core.Repositories;

namespace Ocr.Core.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public UnitOfWork(AppDbContext db) { _db = db; }

    private IRecordRepository? _records;
    public IRecordRepository Records => _records ??= new RecordRepository(_db);

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();
    public Task<int> SaveChangesAsync(System.Threading.CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
