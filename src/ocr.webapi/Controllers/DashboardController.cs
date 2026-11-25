using Microsoft.AspNetCore.Mvc;
using Ocr.Application.Services.Dashboard;
using Ocr.ViewModel.Dashboard;

namespace Ocr.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(
        IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats(
        CancellationToken ct)
    {
        var result =
            await _dashboardService
                .GetStatsAsync(ct);

        return Ok(result);
    }
}