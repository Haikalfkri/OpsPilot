import { useState } from "react"
import { getErrorMessage } from "@/api/errors"
import { StatusBadge } from "@/components/incidents/IncidentBadges"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useChangeStatus } from "@/hooks/useIncidentMutations"
import { STATUS_ACTIONS } from "@/lib/incident-status"
import type { IncidentDetail, IncidentStatus } from "@/types/incident"

interface StatusActionsProps {
  incident: IncidentDetail
  canWrite: boolean
}

export function StatusActions({ incident, canWrite }: StatusActionsProps) {
  const changeStatus = useChangeStatus(incident.id)

  // Status yang sedang menunggu konfirmasi (null = tidak ada)
  const [pending, setPending] = useState<IncidentStatus | null>(null)

  function run(status: IncidentStatus) {
    changeStatus.mutate(status, { onSettled: () => setPending(null) })
  }

  function choose(status: IncidentStatus) {
    changeStatus.reset() // hapus pesan error dari percobaan sebelumnya

    // Perpindahan yang tidak bisa dibatalkan meminta konfirmasi dulu
    if (STATUS_ACTIONS[status].confirm) setPending(status)
    else run(status)
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Status</CardTitle>
      </CardHeader>

      <CardContent className="space-y-4">
        <StatusBadge status={incident.status} />

        {!canWrite && <p className="text-sm text-muted-foreground">You have read-only access.</p>}

        {canWrite && incident.status === "Closed" && (
          <p className="text-sm text-muted-foreground">This incident is closed and can no longer be changed.</p>
        )}

        {/* Panel konfirmasi */}
        {canWrite && pending && (
          <div role="group" aria-label="Confirm status change" className="space-y-3 rounded-md border p-3">
            <p className="text-sm">{STATUS_ACTIONS[pending].confirm}</p>
            <div className="flex gap-2">
              <Button size="sm" disabled={changeStatus.isPending} onClick={() => run(pending)}>
                {changeStatus.isPending ? "Saving…" : "Confirm"}
              </Button>
              <Button size="sm" variant="outline" disabled={changeStatus.isPending} onClick={() => setPending(null)}>
                Cancel
              </Button>
            </div>
          </div>
        )}

        {/* Tombol: persis status tujuan yang diizinkan backend */}
        {canWrite && !pending && incident.allowedNextStatuses.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {incident.allowedNextStatuses.map((status, index) => (
              <Button
                key={status}
                variant={index === 0 ? "default" : "outline"}
                size="sm"
                disabled={changeStatus.isPending}
                onClick={() => choose(status)}
              >
                {STATUS_ACTIONS[status].label}
              </Button>
            ))}
          </div>
        )}

        {changeStatus.isError && (
          <Alert variant="destructive">
            <AlertDescription>{getErrorMessage(changeStatus.error)}</AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  )
}