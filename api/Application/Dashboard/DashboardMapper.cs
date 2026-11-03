using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Dashboard
{
    public static class DashboardMapper
    {
        private static readonly IncidentSeverity[] SeverityOrder =
            { IncidentSeverity.Critical, IncidentSeverity.High, IncidentSeverity.Medium, IncidentSeverity.Low };

        private static readonly IncidentStatus[] StatusOrder =
            { IncidentStatus.Open, IncidentStatus.Investigating, IncidentStatus.Resolved, IncidentStatus.Closed };

        public static DashboardPeriodDto ToDto(this DashboardPeriod period) =>
            new(period.Days, period.FromUtc, period.ToUtc);

        public static IReadOnlyList<SeverityCountDto> ToSeverityCounts(IReadOnlyDictionary<IncidentSeverity, int> counts) =>
            SeverityOrder.Select(severity => new SeverityCountDto(severity, counts.GetValueOrDefault(severity))).ToList();

        public static IReadOnlyList<StatusCountDto> ToStatusCounts(IReadOnlyDictionary<IncidentStatus, int> counts) =>
            StatusOrder.Select(status => new StatusCountDto(status, counts.GetValueOrDefault(status))).ToList();

        public static IReadOnlyList<TrendPointDto> ToTrendPoints(
        DashboardPeriod period, IEnumerable<DateTimeOffset> createdAt, IEnumerable<DateTimeOffset> resolvedAt)
        {
            var createdPerDay = CountPerUtcDay(createdAt);
            var resolvedPerDay = CountPerUtcDay(resolvedAt);

            return Enumerable.Range(0, period.Days)
                .Select(offset => period.FromDate.AddDays(offset))
                .Select(date => new TrendPointDto(
                    date,
                    createdPerDay.GetValueOrDefault(date),
                    resolvedPerDay.GetValueOrDefault(date)))
                .ToList();
        }

        private static Dictionary<DateOnly, int> CountPerUtcDay(IEnumerable<DateTimeOffset> times) =>
            times
                .GroupBy(time => DateOnly.FromDateTime(time.UtcDateTime))
                .ToDictionary(group => group.Key, group => group.Count());
    }
}
