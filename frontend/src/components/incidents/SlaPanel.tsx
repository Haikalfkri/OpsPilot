import { SlaStateBadge } from "@/components/incidents/IncidentBadges"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { formatDateTime, formatRelative } from "@/lib/format"
import type { IncidentDetail, SlaState } from "@/types/incident"

interface TargetRowProps {
  label: string
  state: SlaState
  dueAtUtc: string
  doneLabel: string
  doneAtUtc: string | null
}

function TargetRow({ label, state, dueAtUtc, doneLabel, doneAtUtc }: TargetRowProps) {
  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium">{label}</span>
        <SlaStateBadge state={state} />
      </div>
      <p className="text-xs text-muted-foreground">
        Due {formatDateTime(dueAtUtc)}
        {doneAtUtc === null && ` (${formatRelative(dueAtUtc)})`}
      </p>
      <p className="text-xs text-muted-foreground">
        {doneLabel}: {doneAtUtc ? formatDateTime(doneAtUtc) : "—"}
      </p>
    </div>
  )
}

export function SlaPanel({ incident }: { incident: IncidentDetail }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>SLA</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <TargetRow
          label="Response target"
          state={incident.responseSla}
          dueAtUtc={incident.responseDueAtUtc}
          doneLabel="First response"
          doneAtUtc={incident.firstRespondedAtUtc}
        />
        <TargetRow
          label="Resolution target"
          state={incident.resolutionSla}
          dueAtUtc={incident.resolutionDueAtUtc}
          doneLabel="Resolved"
          doneAtUtc={incident.resolvedAtUtc}
        />
      </CardContent>
    </Card>
  )
}