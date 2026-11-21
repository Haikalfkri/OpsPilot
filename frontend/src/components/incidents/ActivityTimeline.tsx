import { useState } from "react"
import { getErrorMessage } from "@/api/errors"
import { Pagination } from "@/components/Pagination"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useIncidentActivities } from "@/hooks/useIncidentDetail"
import { FEED_PAGE_SIZE } from "@/lib/constants"
import { formatDateTime, formatRelative } from "@/lib/format"
import { cn } from "@/lib/utils"

export function ActivityTimeline({ incidentId }: { incidentId: string }) {
  const [page, setPage] = useState(1)
  const activities = useIncidentActivities(incidentId, page)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Activity</CardTitle>
      </CardHeader>

      <CardContent className="space-y-4">
        {activities.isPending && <Skeleton className="h-24 w-full" />}

        {activities.isError && (
          <Alert variant="destructive">
            <AlertDescription className="space-y-2">
              <p>{getErrorMessage(activities.error)}</p>
              <Button variant="outline" size="sm" onClick={() => activities.refetch()}>
                Try again
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {activities.data && activities.data.items.length === 0 && (
          <p className="text-sm text-muted-foreground">No activity yet.</p>
        )}

        {activities.data && activities.data.items.length > 0 && (
          <ol className="space-y-4 border-l pl-4">
            {activities.data.items.map((activity) => {
              const isBreach = activity.type === "ResponseSlaBreached" || activity.type === "ResolutionSlaBreached"

              return (
                <li key={activity.id} className="space-y-0.5">
                  <p className={cn("text-sm", isBreach && "font-medium text-destructive")}>{activity.message}</p>
                  <p className="text-xs text-muted-foreground" title={formatDateTime(activity.createdAtUtc)}>
                    {/* Tanpa pelaku = dilakukan sistem (pemeriksa SLA) */}
                    {activity.actorName ?? "System"} · {formatRelative(activity.createdAtUtc)}
                  </p>
                </li>
              )
            })}
          </ol>
        )}

        {activities.data && activities.data.totalPages > 1 && (
          <Pagination
            page={activities.data.page}
            totalPages={activities.data.totalPages}
            totalCount={activities.data.totalCount}
            pageSize={FEED_PAGE_SIZE}
            disabled={activities.isPlaceholderData}
            onPageChange={setPage}
          />
        )}
      </CardContent>
    </Card>
  )
}