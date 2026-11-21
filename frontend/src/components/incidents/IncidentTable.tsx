import { Link } from "react-router"
import { SeverityBadge, SlaBadge, StatusBadge } from "@/components/incidents/IncidentBadges"
import { Skeleton } from "@/components/ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { formatDateTime, formatRelative, isPast } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { IncidentSummary } from "@/types/incident"

// Batas penyelesaian hanya relevan untuk incident yang masih berjalan
function DueCell({ incident }: { incident: IncidentSummary }) {
  const active = incident.status === "Open" || incident.status === "Investigating"

  if (!active) return <span className="text-muted-foreground">—</span>

  return (
    <span
      title={formatDateTime(incident.resolutionDueAtUtc)}
      className={cn(isPast(incident.resolutionDueAtUtc) && "font-medium text-destructive")}
    >
      {formatRelative(incident.resolutionDueAtUtc)}
    </span>
  )
}

export function IncidentTable({ items, dimmed }: { items: IncidentSummary[]; dimmed: boolean }) {
  return (
    // dimmed = halaman berikutnya sedang dimuat: tabel lama dipudarkan, bukan dikosongkan
    <div className={cn("rounded-md border transition-opacity", dimmed && "opacity-60")}>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Title</TableHead>
            <TableHead>Severity</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Assignee</TableHead>
            <TableHead>SLA</TableHead>
            <TableHead>Created</TableHead>
            <TableHead>Resolution due</TableHead>
          </TableRow>
        </TableHeader>

        <TableBody>
          {items.map((incident) => (
            <TableRow key={incident.id}>
              <TableCell className="max-w-xs">
                <Link to={`/incidents/${incident.id}`} className="font-medium hover:underline">
                  {incident.title}
                </Link>
              </TableCell>
              <TableCell>
                <SeverityBadge severity={incident.severity} />
              </TableCell>
              <TableCell>
                <StatusBadge status={incident.status} />
              </TableCell>
              <TableCell>
                {incident.assigneeName ?? <span className="text-muted-foreground">Unassigned</span>}
              </TableCell>
              <TableCell>
                <SlaBadge response={incident.responseSla} resolution={incident.resolutionSla} />
              </TableCell>
              <TableCell className="whitespace-nowrap">{formatDateTime(incident.createdAtUtc)}</TableCell>
              <TableCell className="whitespace-nowrap">
                <DueCell incident={incident} />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

// Kerangka abu-abu yang tampil saat data pertama kali dimuat
export function IncidentTableSkeleton() {
  return (
    <div className="space-y-2" aria-label="Loading incidents" aria-busy="true">
      {Array.from({ length: 6 }, (_, index) => (
        <Skeleton key={index} className="h-10 w-full" />
      ))}
    </div>
  )
}