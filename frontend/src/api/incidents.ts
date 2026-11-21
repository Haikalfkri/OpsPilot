import { http } from "@/api/http"
import { FEED_PAGE_SIZE } from "@/lib/constants"
import type { PagedResult } from "@/types/api"
import type {
  IncidentActivity,
  IncidentComment,
  IncidentDetail,
  IncidentStatus,
  IncidentSummary,
  ListIncidentsParams,
  UpdateIncidentRequest,
} from "@/types/incident"

const base = (id: string) => `/api/v1/incidents/${encodeURIComponent(id)}`

export function listIncidents(params: ListIncidentsParams, signal?: AbortSignal) {
  return http.get<PagedResult<IncidentSummary>>("/api/v1/incidents", { query: params, signal })
}

export function getIncident(id: string, signal?: AbortSignal) {
  return http.get<IncidentDetail>(base(id), { signal })
}

export function updateIncident(id: string, body: UpdateIncidentRequest) {
  return http.patch<IncidentDetail>(base(id), body)
}

export function changeIncidentStatus(id: string, status: IncidentStatus) {
  return http.post<IncidentDetail>(`${base(id)}/status`, { status })
}

export function assignIncident(id: string, assigneeId: string | null) {
  return http.put<IncidentDetail>(`${base(id)}/assignee`, { assigneeId })
}

export function listActivities(id: string, page: number, signal?: AbortSignal) {
  return http.get<PagedResult<IncidentActivity>>(`${base(id)}/activities`, {
    query: { page, pageSize: FEED_PAGE_SIZE },
    signal,
  })
}

export function listComments(id: string, page: number, signal?: AbortSignal) {
  return http.get<PagedResult<IncidentComment>>(`${base(id)}/comments`, {
    query: { page, pageSize: FEED_PAGE_SIZE },
    signal,
  })
}

export function addComment(id: string, body: string) {
  return http.post<IncidentComment>(`${base(id)}/comments`, { body })
}