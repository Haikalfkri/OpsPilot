import { useState } from "react"
import { getErrorMessage } from "@/api/errors"
import { PostmortemForm } from "@/components/incidents/PostmortemForm"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { usePostmortem } from "@/hooks/usePostmortem"
import { formatDateTime } from "@/lib/format"
import type { IncidentDetail } from "@/types/incident"
import type { Postmortem } from "@/types/postmortem"

// Tampilan baca: empat bagian, penulis, dan waktu
function PostmortemView({ postmortem }: { postmortem: Postmortem }) {
  const sections = [
    { title: "Summary", text: postmortem.summary },
    { title: "Root cause", text: postmortem.rootCause },
    { title: "Impact", text: postmortem.impact },
    { title: "Corrective actions", text: postmortem.correctiveActions },
  ]

  return (
    <div className="space-y-4">
      {sections.map((section) => (
        <div key={section.title} className="space-y-1">
          <h3 className="text-sm font-medium">{section.title}</h3>
          {/* whitespace-pre-wrap menjaga baris baru. React meng-escape teks, jadi isinya aman. */}
          <p className="whitespace-pre-wrap break-words text-sm text-muted-foreground">{section.text}</p>
        </div>
      ))}

      <p className="text-xs text-muted-foreground">
        Written by {postmortem.authorName ?? "Unknown user"} on {formatDateTime(postmortem.createdAtUtc)}
        {postmortem.updatedAtUtc !== postmortem.createdAtUtc && ` · Updated ${formatDateTime(postmortem.updatedAtUtc)}`}
      </p>
    </div>
  )
}

interface PostmortemSectionProps {
  incident: IncidentDetail
  canWrite: boolean // Admin dan Responder
}

export function PostmortemSection({ incident, canWrite }: PostmortemSectionProps) {
  // Aturan backend: postmortem hanya untuk incident Resolved atau Closed
  const available = incident.status === "Resolved" || incident.status === "Closed"

  const postmortem = usePostmortem(incident.id, available)
  const [editing, setEditing] = useState(false)

  if (!available) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Postmortem</CardTitle>
          <CardDescription>Available once the incident is resolved.</CardDescription>
        </CardHeader>
      </Card>
    )
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Postmortem</CardTitle>
      </CardHeader>

      <CardContent className="space-y-4">
        {postmortem.isPending && <Skeleton className="h-24 w-full" />}

        {postmortem.isError && (
          <Alert variant="destructive">
            <AlertDescription className="space-y-3">
              <p>{getErrorMessage(postmortem.error)}</p>
              <Button variant="outline" size="sm" onClick={() => postmortem.refetch()}>
                Try again
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {postmortem.isSuccess &&
          (editing && canWrite ? (
            <PostmortemForm incidentId={incident.id} initial={postmortem.data} onDone={() => setEditing(false)} />
          ) : postmortem.data === null ? (
            // Belum ada
            canWrite ? (
              <div className="space-y-3">
                <p className="text-sm text-muted-foreground">
                  No postmortem yet. Record what happened and what to do about it.
                </p>
                <Button size="sm" onClick={() => setEditing(true)}>
                  Write postmortem
                </Button>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">No postmortem has been written yet.</p>
            )
          ) : (
            // Sudah ada
            <>
              <PostmortemView postmortem={postmortem.data} />
              {canWrite && (
                <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
                  Edit
                </Button>
              )}
            </>
          ))}
      </CardContent>
    </Card>
  )
}