using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Dashboard
{
    public static class DashboardCacheKeys
    {
        public static string Summary(Guid teamId, int days) => $"dashboard:{teamId}:summary:{days}";

        public static string Breakdown(Guid teamId, int days) => $"dashboard:{teamId}:breakdown:{days}";

        public static string Trends(Guid teamId, int days) => $"dashboard:{teamId}:trends:{days}";

        public static IReadOnlyList<string> AllForTeam(Guid teamId) =>
            DashboardPeriods.Allowed
                .SelectMany(days => new[] { Summary(teamId, days), Breakdown(teamId, days), Trends(teamId, days) })
                .ToList();
    }
}
