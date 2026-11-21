// Urutan tetap, dipakai untuk pilihan dropdown dan validasi URL
export const SEVERITIES = ["Critical", "High", "Medium", "Low"] as const
export const STATUSES = ["Open", "Investigating", "Resolved", "Closed"] as const

export type IncidentSeverity = (typeof SEVERITIES)[number]
export type IncidentStatus = (typeof STATUSES)[number]
export type SlaState = "Pending" | "Met" | "Breached"

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

// Jawaban GET /incidents/{id}. Sama dengan IncidentDetailDto di backend.
export interface IncidentDetail {
  id: string
  teamId: string
  title: string
  description: string
  severity: IncidentSeverity
  status: IncidentStatus
  createdById: string
  createdByName: string | null
  assigneeId: string | null
  assigneeName: string | null
  createdAtUtc: string
  updatedAtUtc: string
  firstRespondedAtUtc: string | null
  resolvedAtUtc: string | null
  closedAtUtc: string | null
  responseDueAtUtc: string
  resolutionDueAtUtc: string
  responseSla: SlaState
  resolutionSla: SlaState
  // Status tujuan yang sah dari status sekarang. Backend yang memutuskan.
  allowedNextStatuses: IncidentStatus[]
}

export type ActivityType =
  | "Created"
  | "Updated"
  | "SeverityChanged"
  | "StatusChanged"
  | "AssigneeChanged"
  | "CommentAdded"
  | "Resolved"
  | "Closed"
  | "ResponseSlaBreached"
  | "ResolutionSlaBreached"
  | "PostmortemCreated"
  | "PostmortemUpdated"

  
// Satu baris di timeline. Nama "IncidentActivity" (bukan "Activity") supaya jelas.
export interface IncidentActivity {
  id: string
  type: ActivityType
  message: string
  actorId: string | null // null = dilakukan sistem (mis. pemeriksa SLA)
  actorName: string | null
  createdAtUtc: string
}

// "IncidentComment", bukan "Comment": nama Comment sudah dipakai tipe bawaan browser
export interface IncidentComment {
  id: string
  incidentId: string
  authorId: string
  authorName: string | null
  body: string
  createdAtUtc: string
}

// Semua field opsional: yang tidak dikirim tidak diubah
export interface UpdateIncidentRequest {
  title?: string
  description?: string
  severity?: IncidentSeverity
}