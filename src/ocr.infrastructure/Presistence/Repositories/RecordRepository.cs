using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Ocr.Domain.Models;
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
    r.FrontImageDataUrl,

                BackImageDataUrl =
    r.BackImageDataUrl
            }
        );
    }
    public async Task<DashboardStatistics> GetDashboardStatsAsync(
        CancellationToken ct = default)
    {
        var query = _db.Records
            .AsNoTracking();


        var totalRecords =
            await query.CountAsync(ct);


        // =========================================
        // GENDER - DYNAMIC
        // =========================================

        var rawGenderGroups =
            await query
                .Where(r =>
                    r.Gender != null &&
                    r.Gender.Trim() != "")
                .GroupBy(r => r.Gender)
                .Select(g => new
                {
                    Gender = g.Key!,
                    Count = g.Count()
                })
                .ToListAsync(ct);


        var genderStats =
            rawGenderGroups
                .Select(g => new
                {
                    Gender = NormalizeText(g.Gender),
                    g.Count
                })
                .Where(g =>
                    !string.IsNullOrWhiteSpace(g.Gender))
                .GroupBy(g => g.Gender)
                .Select(g =>
                    new GenderStatistic
                    {
                        Gender = g.Key,

                        Count =
                            g.Sum(x => x.Count),

                        Percentage =
                            totalRecords == 0
                                ? 0
                                : Math.Round(
                                    g.Sum(x => x.Count)
                                    * 100.0
                                    / totalRecords,
                                    1
                                )
                    })
                .OrderByDescending(x => x.Count)
                .ToList();


        var knownGenderCount =
            genderStats.Sum(x => x.Count);
        var governorateGroups =
    await query
        .Where(r =>
            r.Governorate != null &&
            r.Governorate.Trim() != "")
        .GroupBy(r =>
            r.Governorate!.Trim())
        .Select(g => new
        {
            Governorate = g.Key,
            Count = g.Count()
        })
        .ToListAsync(ct);


        var governorateStats =
            governorateGroups
                .Select(g =>
                    new GovernorateStatistic
                    {
                        Governorate =
                            g.Governorate,

                        Count =
                            g.Count,

                        Percentage =
                            totalRecords == 0
                                ? 0
                                : Math.Round(
                                    g.Count * 100.0
                                    / totalRecords,
                                    1
                                )
                    })
                .OrderByDescending(x => x.Count)
                .ToList();


        var knownGovernorateCount =
            governorateStats.Sum(
                x => x.Count
            );


        var unknownGovernorateCount =
            Math.Max(
                totalRecords -
                knownGovernorateCount,
                0
            );

        var unknownGenderCount =
            Math.Max(
                totalRecords - knownGenderCount,
                0
            );


        // =========================================
        // AGE RANGES
        // =========================================

        var today =
            DateOnly.FromDateTime(
                DateTime.UtcNow
            );


        var age16 =
            today.AddYears(-16);

        var age30 =
            today.AddYears(-30);

        var age40 =
            today.AddYears(-40);

        var age50 =
            today.AddYears(-50);

        var age60 =
            today.AddYears(-60);

        var age70 =
            today.AddYears(-70);


        var age0To15 =
            await query.CountAsync(
                r =>
                    r.DateOfBirth != null &&
                    r.DateOfBirth > age16,
                ct
            );


        var age16To29 =
            await query.CountAsync(
                r =>
                    r.DateOfBirth != null &&
                    r.DateOfBirth <= age16 &&
                    r.DateOfBirth > age30,
                ct
            );


        var age30To39 =
            await query.CountAsync(
                r =>
                    r.DateOfBirth != null &&
                    r.DateOfBirth <= age30 &&
                    r.DateOfBirth > age40,
                ct
            );


        var age40To49 =
            await query.CountAsync(
                r =>
                    r.DateOfBirth != null &&
                    r.DateOfBirth <= age40 &&
                    r.DateOfBirth > age50,
                ct
            );


        var age50To59 =
            await query.CountAsync(
                r =>
                    r.DateOfBirth != null &&
                    r.DateOfBirth <= age50 &&
                    r.DateOfBirth > age60,
                ct
            );


        var age60To69 =
            await query.CountAsync(
                r =>
                    r.DateOfBirth != null &&
                    r.DateOfBirth <= age60 &&
                    r.DateOfBirth > age70,
                ct
            );


        var age70Plus =
            await query.CountAsync(
                r =>
                    r.DateOfBirth != null &&
                    r.DateOfBirth <= age70,
                ct
            );


        var ageRanges =
            new List<AgeRangeStatistic>
            {
            new()
            {
                Label = "0-15",
                Count = age0To15
            },

            new()
            {
                Label = "16-29",
                Count = age16To29
            },

            new()
            {
                Label = "30-39",
                Count = age30To39
            },

            new()
            {
                Label = "40-49",
                Count = age40To49
            },

            new()
            {
                Label = "50-59",
                Count = age50To59
            },

            new()
            {
                Label = "60-69",
                Count = age60To69
            },

            new()
            {
                Label = "70+",
                Count = age70Plus
            }
            };


        // =========================================
        // LAST 6 MONTHS
        // =========================================

        var now =
            DateTime.UtcNow;


        var currentMonthStart =
            new DateTime(
                now.Year,
                now.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc
            );


        var recordsByMonth =
            new List<MonthlyRecordStatistic>();


        for (var i = 5; i >= 0; i--)
        {
            var monthStart =
                currentMonthStart.AddMonths(-i);


            var monthEnd =
                monthStart.AddMonths(1);


            var count =
                await query.CountAsync(
                    r =>
                        r.CreatedAtUtc >= monthStart &&
                        r.CreatedAtUtc < monthEnd,
                    ct
                );


            recordsByMonth.Add(
                new MonthlyRecordStatistic
                {
                    Month =
                        monthStart.ToString("yyyy-MM"),

                    Count = count
                }
            );
        }


        var addedThisMonth =
            recordsByMonth.Count > 0
                ? recordsByMonth[^1].Count
                : 0;


        return new DashboardStatistics
        {
            TotalRecords =
         totalRecords,

            UnknownGenderCount =
         unknownGenderCount,

            UnknownGovernorateCount =
         unknownGovernorateCount,

            AddedThisMonth =
         addedThisMonth,

            GenderStats =
         genderStats,

            GovernorateStats =
         governorateStats,

            AgeRanges =
         ageRanges,

            RecordsByMonth =
         recordsByMonth
        };
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
            RecordSearchCriteria criteria,
            CancellationToken ct = default)
    {
        IQueryable<Record> query =
            _db.Records
                .AsNoTracking();


        // =========================================
        // NAME - Contains
        // =========================================

        if (!string.IsNullOrWhiteSpace(criteria.Name))
        {
            var value =
                criteria.Name.Trim();

            query =
                query.Where(r =>
                    r.Name != null
                    &&
                    EF.Functions.Like(
                        r.Name,
                        $"%{value}%"
                    )
                );
        }


        // =========================================
        // NATIONAL ID - Contains
        // =========================================

        if (!string.IsNullOrWhiteSpace(criteria.IdNumber))
        {
            var value =
                criteria.IdNumber
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
                        $"%{value}%"
                    )
                );
        }


        // =========================================
        // ADDRESS - Contains
        // =========================================

        if (!string.IsNullOrWhiteSpace(criteria.Address))
        {
            var value =
                criteria.Address.Trim();

            query =
                query.Where(r =>
                    r.Address != null
                    &&
                    EF.Functions.Like(
                        r.Address,
                        $"%{value}%"
                    )
                );
        }


        // =========================================
        // GENDER - Exact
        // =========================================

        if (!string.IsNullOrWhiteSpace(criteria.Gender))
        {
            var value =
                criteria.Gender.Trim();

            query =
                query.Where(r =>
                    r.Gender == value
                );
        }


        // =========================================
        // DOB - Exact
        // =========================================

        if (criteria.DateOfBirth.HasValue)
        {
            query =
                query.Where(r =>
                    r.DateOfBirth ==
                    criteria.DateOfBirth.Value
                );
        }


        // =========================================
        // AGE
        // Age is derived from DateOfBirth.
        // =========================================

        if (
            criteria.Age.HasValue
            &&
            criteria.Age.Value >= 0
        )
        {
            var today =
                DateOnly.FromDateTime(
                    DateTime.Today
                );

            var newestDob =
                today.AddYears(
                    -criteria.Age.Value
                );

            var oldestDobExclusive =
                today.AddYears(
                    -(criteria.Age.Value + 1)
                );

            query =
                query.Where(r =>
                    r.DateOfBirth.HasValue
                    &&
                    r.DateOfBirth.Value
                        > oldestDobExclusive
                    &&
                    r.DateOfBirth.Value
                        <= newestDob
                );
        }


        // =========================================
        // RELIGION - Exact
        // =========================================

        if (!string.IsNullOrWhiteSpace(criteria.Religion))
        {
            var value =
                criteria.Religion.Trim();

            query =
                query.Where(r =>
                    r.Religion == value
                );
        }


        // =========================================
        // OCCUPATION - Contains
        // =========================================

        if (!string.IsNullOrWhiteSpace(criteria.Profession))
        {
            var value =
                criteria.Profession.Trim();

            query =
                query.Where(r =>
                    r.Profession != null
                    &&
                    EF.Functions.Like(
                        r.Profession,
                        $"%{value}%"
                    )
                );
        }


        // =========================================
        // MARITAL STATUS - Exact
        // =========================================

        if (!string.IsNullOrWhiteSpace(criteria.MaritalStatus))
        {
            var value =
                criteria.MaritalStatus.Trim();

            query =
                query.Where(r =>
                    r.MaritalStatus == value
                );
        }


        // =========================================
        // HUSBAND NAME
        // Uncomment ONLY when Record entity contains
        // HusbandName and DB column exists.
        // =========================================

        /*
        if (!string.IsNullOrWhiteSpace(criteria.HusbandName))
        {
            var value =
                criteria.HusbandName.Trim();

            query =
                query.Where(r =>
                    r.HusbandName != null
                    &&
                    EF.Functions.Like(
                        r.HusbandName,
                        $"%{value}%"
                    )
                );
        }
        */


        // =========================================
        // EXPIRY DATE - Exact
        // =========================================

        if (criteria.EndDate.HasValue)
        {
            query =
                query.Where(r =>
                    r.EndDate ==
                    criteria.EndDate.Value
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
                    (criteria.PageNumber - 1)
                    * criteria.PageSize
                )

                .Take(
                    criteria.PageSize
                )

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
    private static string NormalizeText(
    string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(
            value
                .Where(c =>
                    System.Globalization
                        .CharUnicodeInfo
                        .GetUnicodeCategory(c)
                    !=
                    System.Globalization
                        .UnicodeCategory
                        .Format
                )
                .ToArray()
        )
        .Trim();
    }

}