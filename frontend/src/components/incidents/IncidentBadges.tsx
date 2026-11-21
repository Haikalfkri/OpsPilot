import { Badge } from "@/components/ui/badge"
import { getOverallSla } from "@/lib/sla"
import { cn } from "@/lib/utils"
import type { IncidentSeverity, IncidentStatus, SlaState } from "@/types/incident"

// Pasangan warna terang/gelap supaya terbaca di kedua tema
const severityStyles: Record<IncidentSeverity, string> = {
  Critical: "bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-200",
  High: "bg-orange-100 text-orange-800 dark:bg-orange-950 dark:text-orange-200",
  Medium: "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-200",
  Low: "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200",
}

const statusStyles: Record<IncidentStatus, string> = {
  Open: "bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-200",
  Investigating: "bg-violet-100 text-violet-800 dark:bg-violet-950 dark:text-violet-200",
  Resolved: "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200",
  Closed: "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200",
}

const slaStyles: Record<SlaState, string> = {
  Pending: "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200",
  Met: "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200",
  Breached: "bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-200",
}

const slaLabels: Record<SlaState, string> = {
  Pending: "On track",
  Met: "Met",
  Breached: "Breached",
}

export function SeverityBadge({ severity }: { severity: IncidentSeverity }) {
  return (
    <Badge variant="outline" className={cn("border-transparent", severityStyles[severity])}>
      {severity}
    </Badge>
  )
}

export function StatusBadge({ status }: { status: IncidentStatus }) {
  return (
    <Badge variant="outline" className={cn("border-transparent", statusStyles[status])}>
      {status}
    </Badge>
  )
}

export function SlaBadge({ response, resolution }: { response: SlaState; resolution: SlaState }) {
  const overall = getOverallSla(response, resolution)

  return (
    <Badge
      variant="outline"
      className={cn("border-transparent", slaStyles[overall])}
      title={`Response: ${response} · Resolution: ${resolution}`}
    >
      {slaLabels[overall]}
    </Badge>
  )
}

export function SlaStateBadge({ state }: { state: SlaState }) {
    return (
      <Badge variant="outline" className={cn("border-transparent", slaStyles[state])}>
        {slaLabels[state]}
      </Badge>
    )
}