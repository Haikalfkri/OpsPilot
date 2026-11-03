using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Dashboard
{
    public sealed record DashboardQuery(int Days = DashboardPeriods.Default);

    public sealed record DashboardPeriodDto(int Days, DateTimeOffset FromUtc, DateTimeOffset ToUtc);

    public sealed record DashboardSummaryDto(
        DashboardPeriodDto Period,
        int OpenIncidents,                  
        int CriticalHighOpenIncidents,      
        int SlaBreachedIncidents,           
        int ResolvedInPeriod,
        double? AverageResolutionMinutes,   
        DateTimeOffset GeneratedAtUtc);

    public sealed record SeverityCountDto(IncidentSeverity Severity, int Count);

    public sealed record StatusCountDto(IncidentStatus Status, int Count);

    public sealed record DashboardBreakdownDto(
        DashboardPeriodDto Period,
        IReadOnlyList<SeverityCountDto> BySeverity,
        IReadOnlyList<StatusCountDto> ByStatus,
        DateTimeOffset GeneratedAtUtc);

    public sealed record TrendPointDto(DateOnly Date, int Created, int Resolved);

    public sealed record DashboardTrendsDto(
        DashboardPeriodDto Period,
        IReadOnlyList<TrendPointDto> Points,
        DateTimeOffset GeneratedAtUtc);
}
