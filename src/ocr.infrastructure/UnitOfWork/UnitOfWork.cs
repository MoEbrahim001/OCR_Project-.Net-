using Ocr.Domain.Repositories;
using Ocr.Domain.UnitOfWork;

namespace Ocr.Infrastructure.Persistence.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public IRecordRepository Records { get; }

    public UnitOfWork(
        AppDbContext db,
        IRecordRepository records)
    {
        _db = db;
        Records = records;
    }

    public Task<int> SaveChangesAsync(
        CancellationToken ct = default)
    {
        return _db.SaveChangesAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
    }
}