using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Dashboard
{
    public static class DashboardPeriods
    {
        public const int Default = 7;

        public static readonly IReadOnlyList<int> Allowed = new[] { 7, 14, 30, 90 };
    }

    public sealed record DashboardPeriod(int Days, DateOnly FromDate, DateTimeOffset FromUtc, DateTimeOffset ToUtc)
    {
        public static DashboardPeriod Create(int days, DateTimeOffset nowUtc)
        {
            // tanggal hari ini diambil dari waktu UTC, bukan waktu lokal
            var today = DateOnly.FromDateTime(nowUtc.UtcDateTime);

            // days=1 -> hanya hari ini; days=7 -> 6 hari sebelumnya + hari ini
            var fromDate = today.AddDays(-(days - 1));

            var fromUtc = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

            return new DashboardPeriod(days, fromDate, fromUtc, nowUtc);
        }
    }
}