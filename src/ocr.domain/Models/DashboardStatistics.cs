namespace Ocr.Domain.Models;

public class DashboardStatistics
{
    public int TotalRecords { get; set; }

    public int UnknownGenderCount { get; set; }

    public int UnknownGovernorateCount { get; set; }

    public int AddedThisMonth { get; set; }

    public List<GenderStatistic> GenderStats { get; set; } = [];

    public List<GovernorateStatistic> GovernorateStats { get; set; } = [];

    public List<AgeRangeStatistic> AgeRanges { get; set; } = [];

    public List<MonthlyRecordStatistic> RecordsByMonth { get; set; } = [];
}


public class GenderStatistic
{
    public string Gender { get; set; } = string.Empty;

    public int Count { get; set; }

    public double Percentage { get; set; }
}


public class GovernorateStatistic
{
    public string Governorate { get; set; } = string.Empty;

    public int Count { get; set; }

    public double Percentage { get; set; }
}


public class AgeRangeStatistic
{
    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }
}


public class MonthlyRecordStatistic
{
    public string Month { get; set; } = string.Empty;

    public int Count { get; set; }
}