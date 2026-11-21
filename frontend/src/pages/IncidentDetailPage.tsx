import { useState } from "react"
import { Link, useParams } from "react-router"
import { getErrorMessage, isApiError } from "@/api/errors"
import { ActivityTimeline } from "@/components/incidents/ActivityTimeline"
import { AssigneeControl } from "@/components/incidents/AssigneeControl"
import { CommentsSection } from "@/components/incidents/CommentsSection"
import { SeverityBadge, StatusBadge } from "@/components/incidents/IncidentBadges"
import { IncidentEditForm } from "@/components/incidents/IncidentEditForm"
import { SlaPanel } from "@/components/incidents/SlaPanel"
import { StatusActions } from "@/components/incidents/StatusActions"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useAuth } from "@/hooks/useAuth"
import { useIncident } from "@/hooks/useIncidentDetail"
import { formatDateTime } from "@/lib/format"
import { canManageIncidents } from "@/lib/permissions"
import { FlashNotice } from "@/components/FlashNotice"

function BackLink() {
  return (
    <Link to="/incidents" className="text-sm text-muted-foreground hover:underline">
      ← Back to incidents
    </Link>
  )
}

export default function IncidentDetailPage() {
  const { id = "" } = useParams<{ id: string }>()
  const { user } = useAuth()
  const incident = useIncident(id)
  const [editing, setEditing] = useState(false)

  if (incident.isPending) {
    return (
      <div className="space-y-4" aria-busy="true" aria-label="Loading incident">
        <Skeleton className="h-6 w-40" />
        <Skeleton className="h-40 w-full" />
        <Skeleton className="h-40 w-full" />
      </div>
    )
  }

  if (incident.isError) {
    // 404 = tidak ada, atau milik tim lain (backend sengaja tidak membedakan keduanya)
    const notFound = isApiError(incident.error) && incident.error.status === 404

    return (
      <div className="space-y-4">
        <BackLink />
        {notFound ? (
          <div className="space-y-3">
            <h1 className="text-2xl font-semibold">Incident not found</h1>
            <p className="text-sm text-muted-foreground">It does not exist, or you do not have access to it.</p>
            <Link to="/incidents" className={buttonVariants({ variant: "outline" })}>
              Back to incidents
            </Link>
          </div>
        ) : (
          <Alert variant="destructive">
            <AlertTitle>Could not load the incident</AlertTitle>
            <AlertDescription className="space-y-3">
              <p>{getErrorMessage(incident.error)}</p>
              <Button variant="outline" size="sm" onClick={() => incident.refetch()}>
                Try again
              </Button>
            </AlertDescription>
          </Alert>
        )}
      </div>
    )
  }

  const data = incident.data
  const canWrite = user ? canManageIncidents(user.role) : false

  // Closed tidak bisa diubah sama sekali (backend menolaknya dengan 409), jadi tidak ditawarkan
  const canEdit = canWrite && data.status !== "Closed"

  return (
    <div className="space-y-6">
      <BackLink />
      <FlashNotice />
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Card>
            <CardHeader className="space-y-3">
              <div className="flex flex-wrap items-center gap-2">
                <SeverityBadge severity={data.severity} />
                <StatusBadge status={data.status} />
              </div>

              {!(editing && canEdit) && <CardTitle className="text-2xl">{data.title}</CardTitle>}
            </CardHeader>

            <CardContent className="space-y-4">
              {editing && canEdit ? (
                <IncidentEditForm incident={data} onDone={() => setEditing(false)} />
              ) : (
                <>
                  <p className="whitespace-pre-wrap break-words text-sm">{data.description}</p>
                  {canEdit && (
                    <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
                      Edit
                    </Button>
                  )}
                </>
              )}
            </CardContent>
          </Card>

          <CommentsSection incidentId={data.id} canWrite={canWrite} />
          <ActivityTimeline incidentId={data.id} />
        </div>

        <div className="space-y-6">
          <StatusActions incident={data} canWrite={canWrite} />

          <Card>
            <CardHeader>
              <CardTitle>Details</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <AssigneeControl incident={data} canEdit={canEdit} />

              <dl className="space-y-2 text-sm">
                <div className="flex justify-between gap-4">
                  <dt className="text-muted-foreground">Created by</dt>
                  <dd>{data.createdByName ?? "Unknown user"}</dd>
                </div>
                <div className="flex justify-between gap-4">
                  <dt className="text-muted-foreground">Created</dt>
                  <dd>{formatDateTime(data.createdAtUtc)}</dd>
                </div>
                <div className="flex justify-between gap-4">
                  <dt className="text-muted-foreground">Last updated</dt>
                  <dd>{formatDateTime(data.updatedAtUtc)}</dd>
                </div>
                {data.closedAtUtc && (
                  <div className="flex justify-between gap-4">
                    <dt className="text-muted-foreground">Closed</dt>
                    <dd>{formatDateTime(data.closedAtUtc)}</dd>
                  </div>
                )}
              </dl>
            </CardContent>
          </Card>

          <SlaPanel incident={data} />
        </div>
      </div>
    </div>
  )
}