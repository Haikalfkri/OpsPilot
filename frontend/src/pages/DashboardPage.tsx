import { Link, useSearchParams } from "react-router"
import { BreakdownCard } from "@/components/dashboard/BreakdownCard"
import { StatCard } from "@/components/dashboard/StatCard"
import { TrendCard } from "@/components/dashboard/TrendCard"
import { IncidentTable } from "@/components/incidents/IncidentTable"
import { QueryState } from "@/components/QueryState"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useDashboardBreakdown, useDashboardSummary, useDashboardTrends } from "@/hooks/useDashboard"
import { useIncidents } from "@/hooks/useIncidents"
import { RECENT_INCIDENTS_COUNT } from "@/lib/constants"
import { usePageTitle } from "@/hooks/usePageTitle"
import {
  describeCounts,
  formatAverageResolution,
  oldestGeneratedAt,
  parseDashboardDays,
  periodSearchParams,
  severityData,
  statusData,
} from "@/lib/dashboard"
import { formatRelative } from "@/lib/format"
import { DASHBOARD_PERIODS } from "@/types/dashboard"
import type { ListIncidentsParams } from "@/types/incident"

// Incident terbaru: memakai ulang daftar incident (5 yang paling baru dibuat)
const RECENT_PARAMS: ListIncidentsParams = {
  page: 1,
  pageSize: RECENT_INCIDENTS_COUNT,
  sortBy: "createdAt",
  sortDirection: "desc",
}

export default function DashboardPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  usePageTitle("Dashboard")

  // URL -> periode. URL adalah satu-satunya sumber kebenaran.
  const days = parseDashboardDays(searchParams)

  // Empat pemanggilan terpisah: yang gagal tidak merusak yang lain
  const summary = useDashboardSummary(days)
  const breakdown = useDashboardBreakdown(days)
  const trends = useDashboardTrends(days)
  const recent = useIncidents(RECENT_PARAMS)

  // Umur angka: yang paling lama di antara ketiganya
  const stamps: string[] = []
  if (summary.data) stamps.push(summary.data.generatedAtUtc)
  if (breakdown.data) stamps.push(breakdown.data.generatedAtUtc)
  if (trends.data) stamps.push(trends.data.generatedAtUtc)
  const generatedAt = oldestGeneratedAt(stamps)

  const isRefreshing = summary.isFetching || breakdown.isFetching || trends.isFetching

  function refresh() {
    void summary.refetch()
    void breakdown.refetch()
    void trends.refetch()
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Dashboard</h1>
          <p className="text-sm text-muted-foreground">
            {generatedAt ? `Updated ${formatRelative(generatedAt)}. ` : ""}
            Figures refresh every minute and can lag by up to a minute.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <div role="group" aria-label="Period" className="flex gap-2">
            {DASHBOARD_PERIODS.map((period) => (
              <Button
                key={period}
                size="sm"
                variant={period === days ? "default" : "outline"}
                aria-pressed={period === days}
                onClick={() => setSearchParams(periodSearchParams(period))}
              >
                {period} days
              </Button>
            ))}
          </div>

          <Button size="sm" variant="ghost" disabled={isRefreshing} onClick={refresh}>
            {isRefreshing ? "Refreshing…" : "Refresh"}
          </Button>
        </div>
      </div>

      {/* Kartu angka */}
      <QueryState query={summary} skeletonClassName="h-32" errorTitle="Could not load the key figures">
        {(data) => (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
            <StatCard title="Open incidents" value={String(data.openIncidents)} hint="Open and investigating" />
            <StatCard
              title="Critical / High open"
              value={String(data.criticalHighOpenIncidents)}
              hint="Of the open incidents"
            />
            <StatCard
              title="SLA breached"
              value={String(data.slaBreachedIncidents)}
              hint="Open incidents past a target"
              tone={data.slaBreachedIncidents > 0 ? "danger" : "default"}
            />
            <StatCard title="Resolved" value={String(data.resolvedInPeriod)} hint={`In the last ${days} days`} />
            <StatCard
              title="Avg. resolution time"
              value={formatAverageResolution(data.averageResolutionMinutes)}
              hint="For incidents resolved in this period"
            />
          </div>
        )}
      </QueryState>

      {/* Per severity dan per status */}
      <QueryState query={breakdown} skeletonClassName="h-72" errorTitle="Could not load the breakdown">
        {(data) => {
          const bySeverity = severityData(data.bySeverity)
          const byStatus = statusData(data.byStatus)

          return (
            <div className="grid gap-4 lg:grid-cols-2">
              <BreakdownCard
                title="By severity"
                data={bySeverity}
                color="var(--chart-1)"
                ariaLabel={`Incidents created in the last ${days} days, by severity: ${describeCounts(bySeverity)}`}
                emptyMessage="No incidents were created in this period."
              />
              <BreakdownCard
                title="By status"
                data={byStatus}
                color="var(--chart-2)"
                ariaLabel={`Incidents created in the last ${days} days, by status: ${describeCounts(byStatus)}`}
                emptyMessage="No incidents were created in this period."
              />
            </div>
          )
        }}
      </QueryState>

      {/* Tren per hari */}
      <QueryState query={trends} skeletonClassName="h-80" errorTitle="Could not load the trend">
        {(data) => <TrendCard points={data.points} days={days} />}
      </QueryState>

      {/* Incident terbaru */}
      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle>Recent incidents</CardTitle>
          <Link to="/incidents" className="text-sm text-muted-foreground hover:underline">
            View all
          </Link>
        </CardHeader>

        <CardContent>
          <QueryState query={recent} skeletonClassName="h-40" errorTitle="Could not load recent incidents">
            {(page) =>
              page.items.length === 0 ? (
                <p className="py-6 text-center text-sm text-muted-foreground">No incidents yet.</p>
              ) : (
                <IncidentTable items={page.items} dimmed={false} />
              )
            }
          </QueryState>
        </CardContent>
      </Card>
    </div>
  )
}