using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Ocr.Domain.Repositories;
using Ocr.Model.Entities;

namespace Ocr.Infrastructure.Persistence.Repositories;

public class RecordRepository : IRecordRepository
{
    private readonly AppDbContext _db;

    private const string ImageExistsMarker =
        "__HAS_IMAGE__";

    public RecordRepository(AppDbContext db)
    {
        _db = db;
    }


    // =====================================================
    // GET BY ID
    // Full record including images.
    // Use only when we REALLY need the images.
    // =====================================================

    public async Task<Record?> GetByIdAsync(
        int id,
        CancellationToken ct = default)
    {
        return await _db.Records
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == id,
                ct
            );
    }


    // =====================================================
    // ADD
    // =====================================================

    public async Task AddAsync(
        Record record,
        CancellationToken ct = default)
    {
        await _db.Records.AddAsync(
            record,
            ct
        );
    }


    // =====================================================
    // UPDATE
    //
    // IMPORTANT:
    // Do not call DbSet.Update().
    //
    // Update() marks ALL properties modified including fields
    // we don't want to touch.
    //
    // Attach a lightweight entity and explicitly mark only
    // editable fields.
    // =====================================================

    public void Update(Record record)
    {
        var entry = _db.Entry(record);

        if (entry.State == EntityState.Detached)
        {
            _db.Records.Attach(record);
        }

        // Normal editable fields
        entry.Property(r => r.Name)
            .IsModified = true;

        entry.Property(r => r.IdNumber)
            .IsModified = true;

        entry.Property(r => r.DateOfBirth)
            .IsModified = true;

        entry.Property(r => r.Address)
            .IsModified = true;

        entry.Property(r => r.Gender)
            .IsModified = true;

        entry.Property(r => r.Profession)
            .IsModified = true;

        entry.Property(r => r.MaritalStatus)
            .IsModified = true;

        entry.Property(r => r.Religion)
            .IsModified = true;

        entry.Property(r => r.EndDate)
            .IsModified = true;


        // =========================================
        // Large fields:
        // update ONLY when a new value was supplied.
        // =========================================

        if (record.PhotoBase64 != null)
        {
            entry.Property(r => r.PhotoBase64)
                .IsModified = true;
        }

        if (record.FaceBase64 != null)
        {
            entry.Property(r => r.FaceBase64)
                .IsModified = true;
        }

        if (record.FrontImageDataUrl != null)
        {
            entry.Property(r => r.FrontImageDataUrl)
                .IsModified = true;
        }

        if (record.BackImageDataUrl != null)
        {
            entry.Property(r => r.BackImageDataUrl)
                .IsModified = true;
        }

        if (record.Notes != null)
        {
            entry.Property(r => r.Notes)
                .IsModified = true;
        }
    }

    // =====================================================
    // DELETE
    //
    // Works with stub entity:
    // new Record { Id = id }
    //
    // So DELETE does NOT need SELECT first.
    // =====================================================

    public void Delete(Record record)
    {
        var entry =
            _db.Entry(record);

        if (entry.State ==
            EntityState.Detached)
        {
            _db.Records.Attach(record);
        }

        _db.Records.Remove(record);
    }


    // =====================================================
    // LIGHTWEIGHT LIST PROJECTION
    //
    // IMPORTANT:
    // Do NOT send image Base64 in table queries.
    //
    // We only send "__HAS_IMAGE__" when an image exists.
    // =====================================================

    private static IQueryable<Record>
        SelectForList(
            IQueryable<Record> query)
    {
        return query.Select(r =>
            new Record
            {
                Id = r.Id,

                Name = r.Name,

                IdNumber = r.IdNumber,

                DateOfBirth =
                    r.DateOfBirth,

                Address =
                    r.Address,

                Gender =
                    r.Gender,

                Profession =
                    r.Profession,

                MaritalStatus =
                    r.MaritalStatus,

                Religion =
                    r.Religion,

                EndDate =
                    r.EndDate,

                Notes =
                    r.Notes,

                CreatedAtUtc =
                    r.CreatedAtUtc,


                // Do NOT load these big fields.

                PhotoBase64 = null,

                FaceBase64 = null,


                // Just tell Angular that
                // the image exists.

                FrontImageDataUrl =
                    r.FrontImageDataUrl == null
                        ? null
                        : ImageExistsMarker,

                BackImageDataUrl =
                    r.BackImageDataUrl == null
                        ? null
                        : ImageExistsMarker
            }
        );
    }


    // =====================================================
    // PAGINATION
    // =====================================================

    public async Task<(
        IReadOnlyList<Record> Items,
        int TotalCount)>
        GetPagedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
    {
        var query =
            _db.Records
                .AsNoTracking();


        var totalCount =
            await query.CountAsync(ct);


        var items =
            await SelectForList(query)

                .OrderByDescending(
                    r => r.Id
                )

                .Skip(
                    (pageNumber - 1)
                    * pageSize
                )

                .Take(pageSize)

                .ToListAsync(ct);


        return (
            items,
            totalCount
        );
    }


    // =====================================================
    // SEARCH
    // =====================================================

    public async Task<(
        IReadOnlyList<Record> Items,
        int TotalCount)>
        SearchAsync(
            string? name,
            string? idNumber,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
    {
        IQueryable<Record> query =
            _db.Records
                .AsNoTracking();


        // =================================================
        // NAME
        // =================================================

        if (!string.IsNullOrWhiteSpace(name))
        {
            var nameTerm =
                name.Trim();

            query =
                query.Where(r =>

                    r.Name != null

                    &&

                    EF.Functions.Like(
                        r.Name,
                        $"%{nameTerm}%"
                    )
                );
        }


        // =================================================
        // NATIONAL ID
        // =================================================

        if (!string.IsNullOrWhiteSpace(idNumber))
        {
            var idTerm =
                idNumber
                    .Trim()
                    .Replace(" ", "");


            query =
                query.Where(r =>

                    r.IdNumber != null

                    &&

                    EF.Functions.Like(
                        r.IdNumber.Replace(
                            " ",
                            ""
                        ),
                        $"%{idTerm}%"
                    )
                );
        }


        var totalCount =
            await query.CountAsync(ct);


        var items =
            await SelectForList(query)

                .OrderByDescending(
                    r => r.CreatedAtUtc
                )

                .Skip(
                    (pageNumber - 1)
                    * pageSize
                )

                .Take(pageSize)

                .ToListAsync(ct);


        return (
            items,
            totalCount
        );
    }
    public async Task<IReadOnlyList<Record>> GetForExportAsync(
    string? name,
    string? idNumber,
    CancellationToken ct = default)
    {
        IQueryable<Record> query =
            _db.Records
                .AsNoTracking();


        // =====================================================
        // NAME FILTER
        // =====================================================

        if (!string.IsNullOrWhiteSpace(name))
        {
            var nameTerm =
                name.Trim();

            query =
                query.Where(r =>
                    r.Name != null
                    &&
                    EF.Functions.Like(
                        r.Name,
                        $"%{nameTerm}%"
                    )
                );
        }


        // =====================================================
        // NATIONAL ID FILTER
        // =====================================================

        if (!string.IsNullOrWhiteSpace(idNumber))
        {
            var idTerm =
                idNumber
                    .Trim()
                    .Replace(" ", "");


            query =
                query.Where(r =>
                    r.IdNumber != null
                    &&
                    EF.Functions.Like(
                        r.IdNumber.Replace(
                            " ",
                            ""
                        ),
                        $"%{idTerm}%"
                    )
                );
        }


        // =====================================================
        // IMPORTANT:
        // Lightweight projection.
        // NO images.
        // =====================================================

        return await query

            .OrderByDescending(
                r => r.CreatedAtUtc
            )

            .Select(r =>
                new Record
                {
                    Id =
                        r.Id,

                    Name =
                        r.Name,

                    IdNumber =
                        r.IdNumber,

                    Address =
                        r.Address,

                    DateOfBirth =
                        r.DateOfBirth,

                    Gender =
                        r.Gender,

                    Profession =
                        r.Profession,

                    Religion =
                        r.Religion,

                    MaritalStatus =
                        r.MaritalStatus,

                    EndDate =
                        r.EndDate,

                    CreatedAtUtc =
                        r.CreatedAtUtc
                }
            )

            .ToListAsync(ct);
    }


    // =====================================================
    // EXPORT SELECTED RECORDS BY DATABASE IDS
    // =====================================================

    public async Task<IReadOnlyList<Record>> GetForExportByIdsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken ct = default)
    {
        if (
            ids == null
            ||
            ids.Count == 0
        )
        {
            return Array.Empty<Record>();
        }


        return await _db.Records

            .AsNoTracking()

            // Only records selected by Angular checkboxes.
            .Where(
                r => ids.Contains(r.Id)
            )

            .OrderByDescending(
                r => r.CreatedAtUtc
            )

            // Lightweight export projection.
            // Do NOT load Base64 / ID images.
            .Select(
                r => new Record
                {
                    Id =
                        r.Id,

                    Name =
                        r.Name,

                    IdNumber =
                        r.IdNumber,

                    Address =
                        r.Address,

                    DateOfBirth =
                        r.DateOfBirth,

                    Gender =
                        r.Gender,

                    Profession =
                        r.Profession,

                    Religion =
                        r.Religion,

                    MaritalStatus =
                        r.MaritalStatus,

                    EndDate =
                        r.EndDate,

                    CreatedAtUtc =
                        r.CreatedAtUtc
                }
            )

            .ToListAsync(ct);
    }

}