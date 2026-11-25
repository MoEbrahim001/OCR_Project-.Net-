using Ocr.Domain.UnitOfWork;
using Ocr.ViewModel.Dashboard;

namespace Ocr.Application.Services.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;


    public DashboardService(
        IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }


    public async Task<DashboardStatsDto> GetStatsAsync(
        CancellationToken ct = default)
    {
        var stats =
            await _unitOfWork
                .Records
                .GetDashboardStatsAsync(ct);


        return new DashboardStatsDto
        {
            TotalRecords =
                stats.TotalRecords,

            UnknownGenderCount =
                stats.UnknownGenderCount,

            UnknownGovernorateCount =
                stats.UnknownGovernorateCount,

            AddedThisMonth =
                stats.AddedThisMonth,


            GenderStats =
                stats.GenderStats
                    .Select(x =>
                        new GenderStatDto
                        {
                            Gender =
                                x.Gender,

                            Count =
                                x.Count,

                            Percentage =
                                x.Percentage
                        })
                    .ToList(),


            GovernorateStats =
                stats.GovernorateStats
                    .Select(x =>
                        new GovernorateStatDto
                        {
                            Governorate =
                                x.Governorate,

                            Count =
                                x.Count,

                            Percentage =
                                x.Percentage
                        })
                    .ToList(),


            AgeRanges =
                stats.AgeRanges
                    .Select(x =>
                        new AgeRangeStatDto
                        {
                            Label =
                                x.Label,

                            Count =
                                x.Count
                        })
                    .ToList(),


            RecordsByMonth =
                stats.RecordsByMonth
                    .Select(x =>
                        new MonthlyRecordStatDto
                        {
                            Month =
                                x.Month,

                            Count =
                                x.Count
                        })
                    .ToList()
        };
    }
}
