namespace Ocr.ViewModel.Dashboard;

public class DashboardStatsDto
{
    public int TotalRecords { get; set; }

    public int UnknownGenderCount { get; set; }

    public int UnknownGovernorateCount { get; set; }

    public int AddedThisMonth { get; set; }

    public List<GenderStatDto> GenderStats { get; set; } = [];

    public List<GovernorateStatDto> GovernorateStats { get; set; } = [];

    public List<AgeRangeStatDto> AgeRanges { get; set; } = [];

    public List<MonthlyRecordStatDto> RecordsByMonth { get; set; } = [];
}


public class GenderStatDto
{
    public string Gender { get; set; } = string.Empty;

    public int Count { get; set; }

    public double Percentage { get; set; }
}


public class GovernorateStatDto
{
    public string Governorate { get; set; } = string.Empty;

    public int Count { get; set; }

    public double Percentage { get; set; }
}


public class AgeRangeStatDto
{
    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }
}


public class MonthlyRecordStatDto
{
    public string Month { get; set; } = string.Empty;

    public int Count { get; set; }
}