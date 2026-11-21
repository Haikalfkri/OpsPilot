export type IncidentSeverity = "Critical" | "High" | "Medium" | "Low"
export type IncidentStatus = "Open" | "Investigating" | "Resolved" | "Closed"
export type SlaState = "Pending" | "Met" | "Breached"

// Urutan tetap, dipakai untuk pilihan dropdown dan validasi URL
export const SEVERITIES: readonly IncidentSeverity[] = ["Critical", "High", "Medium", "Low"]
export const STATUSES: readonly IncidentStatus[] = ["Open", "Investigating", "Resolved", "Closed"]

// Satu baris di daftar. Bentuknya sama dengan IncidentSummaryDto di backend.
export interface IncidentSummary {
  id: string
  title: string
  severity: IncidentSeverity
  status: IncidentStatus
  assigneeId: string | null
  assigneeName: string | null
  createdAtUtc: string
  updatedAtUtc: string
  firstRespondedAtUtc: string | null
  resolvedAtUtc: string | null
  responseDueAtUtc: string
  resolutionDueAtUtc: string
  responseSla: SlaState
  resolutionSla: SlaState
}

// Kolom yang boleh dipakai untuk sorting (daftar izin yang sama dengan backend)
export type SortBy = "createdAt" | "updatedAt" | "severity" | "dueAt"

// Parameter query untuk GET /incidents.
// Ditulis sebagai "type" (bukan "interface") supaya bisa dikirim sebagai kamus query.
export type ListIncidentsParams = {
  page: number
  pageSize: number
  status?: IncidentStatus
  severity?: IncidentSeverity
  assigneeId?: string
  createdFrom?: string // ISO 8601 (UTC)
  createdTo?: string
  search?: string
  sortBy: SortBy
  sortDirection: "asc" | "desc"
}