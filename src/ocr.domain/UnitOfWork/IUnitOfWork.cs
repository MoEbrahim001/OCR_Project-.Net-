using Ocr.Domain.Repositories;

namespace Ocr.Domain.UnitOfWork;

public interface IUnitOfWork : IAsyncDisposable
{
    IRecordRepository Records { get; }
    Task<int> SaveChangesAsync(System.Threading.CancellationToken ct = default);
}
