using Ocr.ViewModel.Dashboard;

namespace Ocr.Application.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(
        CancellationToken ct = default);
}