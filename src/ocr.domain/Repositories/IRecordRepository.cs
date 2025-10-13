using Ocr.Model.Entities;
using System.Linq.Expressions;

namespace Ocr.Domain.Repositories;

public interface IRecordRepository
{
    Task<Record?> GetByIdAsync(int id);
    Task AddAsync(Record record, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task UpdateAsync(Record entity);
    Task DeleteAsync(Record entity);
    IQueryable<Record> Query(Expression<Func<Record, bool>>? filter = null);
}
