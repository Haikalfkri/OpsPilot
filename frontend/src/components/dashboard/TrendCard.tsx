import { TrendLineChart } from "@/components/dashboard/DashboardCharts"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { sumTrend } from "@/lib/dashboard"
import type { TrendPoint } from "@/types/dashboard"

export function TrendCard({ points, days }: { points: TrendPoint[]; days: number }) {
  const { created, resolved } = sumTrend(points)
  const summary = `Created ${created} · Resolved ${resolved} in the last ${days} days`

  return (
    <Card>
      <CardHeader>
        <CardTitle>Created vs resolved</CardTitle>
        <CardDescription>Per day (UTC)</CardDescription>
      </CardHeader>

      <CardContent className="space-y-3">
        {created === 0 && resolved === 0 ? (
          <p className="py-10 text-center text-sm text-muted-foreground">
            Nothing was created or resolved in this period.
          </p>
        ) : (
          <>
            <TrendLineChart points={points} ariaLabel={`Incidents created and resolved per day. ${summary}`} />
            <p className="text-sm text-muted-foreground">{summary}</p>
          </>
        )}
      </CardContent>
    </Card>
  )
}