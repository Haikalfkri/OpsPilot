import {
  SEVERITIES,
  STATUSES,
  type IncidentSeverity,
  type IncidentStatus,
  type ListIncidentsParams,
  type SortBy,
} from "@/types/incident"

export const PAGE_SIZE = 20

// Pilihan urutan di UI -> parameter sortBy dan sortDirection untuk backend
export const SORT_OPTIONS = [
  { key: "newest", label: "Newest first", sortBy: "createdAt", sortDirection: "desc" },
  { key: "oldest", label: "Oldest first", sortBy: "createdAt", sortDirection: "asc" },
  { key: "updated", label: "Recently updated", sortBy: "updatedAt", sortDirection: "desc" },
  { key: "severity", label: "Severity (high to low)", sortBy: "severity", sortDirection: "desc" },
  { key: "due", label: "Due soonest", sortBy: "dueAt", sortDirection: "asc" },
] as const satisfies readonly {
  key: string
  label: string
  sortBy: SortBy
  sortDirection: "asc" | "desc"
}[]

export type SortKey = (typeof SORT_OPTIONS)[number]["key"]

const DEFAULT_SORT: SortKey = "newest"

// Keadaan filter di halaman, persis seperti yang tertulis di URL
export interface IncidentFilters {
  page: number
  status?: IncidentStatus
  severity?: IncidentSeverity
  assigneeId?: string
  from?: string // tanggal lokal "YYYY-MM-DD"
  to?: string
  search: string // "" kalau tidak ada
  sort: SortKey
}

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/
const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// Mengembalikan nilai kalau termasuk daftar yang diizinkan, selain itu undefined
function oneOf<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((candidate) => candidate === value)
}

function validDate(value: string | null): string | undefined {
  return value && DATE_PATTERN.test(value) && !Number.isNaN(Date.parse(value)) ? value : undefined
}

// URL -> filter. Nilai yang tidak valid diabaikan dan jatuh ke bawaan.
export function parseFilters(params: URLSearchParams): IncidentFilters {
  const page = Number.parseInt(params.get("page") ?? "", 10)
  const assignee = params.get("assignee")

  return {
    page: Number.isInteger(page) && page >= 1 ? page : 1,
    status: oneOf(params.get("status"), STATUSES),
    severity: oneOf(params.get("severity"), SEVERITIES),
    assigneeId: assignee && GUID_PATTERN.test(assignee) ? assignee : undefined,
    from: validDate(params.get("from")),
    to: validDate(params.get("to")),
    search: (params.get("q") ?? "").trim().slice(0, 100), // batas sama dengan backend
    sort:
      oneOf(
        params.get("sort"),
        SORT_OPTIONS.map((option) => option.key),
      ) ?? DEFAULT_SORT,
  }
}

// Filter -> URL. Nilai bawaan tidak ditulis, supaya URL tetap pendek.
export function toSearchParams(filters: IncidentFilters): URLSearchParams {
  const params = new URLSearchParams()

  if (filters.status) params.set("status", filters.status)
  if (filters.severity) params.set("severity", filters.severity)
  if (filters.assigneeId) params.set("assignee", filters.assigneeId)
  if (filters.from) params.set("from", filters.from)
  if (filters.to) params.set("to", filters.to)
  if (filters.search) params.set("q", filters.search)
  if (filters.sort !== DEFAULT_SORT) params.set("sort", filters.sort)
  if (filters.page > 1) params.set("page", String(filters.page))

  return params
}

// "2026-10-30" (tanggal lokal) -> awal hari itu di zona waktu pengguna -> ISO UTC
export function startOfDayIso(date: string): string {
  return new Date(`${date}T00:00:00`).toISOString()
}

// ... dan akhir hari itu (supaya incident yang dibuat sore hari ikut tercakup)
export function endOfDayIso(date: string): string {
  return new Date(`${date}T23:59:59.999`).toISOString()
}

// Filter -> parameter untuk backend
export function toApiParams(filters: IncidentFilters): ListIncidentsParams {
  const sort = SORT_OPTIONS.find((option) => option.key === filters.sort) ?? SORT_OPTIONS[0]

  return {
    page: filters.page,
    pageSize: PAGE_SIZE,
    status: filters.status,
    severity: filters.severity,
    assigneeId: filters.assigneeId,
    createdFrom: filters.from ? startOfDayIso(filters.from) : undefined,
    createdTo: filters.to ? endOfDayIso(filters.to) : undefined,
    search: filters.search || undefined,
    sortBy: sort.sortBy,
    sortDirection: sort.sortDirection,
  }
}

// Apakah ada filter yang aktif? (urutan dan halaman tidak dihitung)
export function hasActiveFilters(filters: IncidentFilters): boolean {
  return Boolean(
    filters.status || filters.severity || filters.assigneeId || filters.from || filters.to || filters.search,
  )
}