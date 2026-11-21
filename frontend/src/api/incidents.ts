import { http } from "@/api/http"
import type { PagedResult } from "@/types/api"
import type { IncidentSummary, ListIncidentsParams } from "@/types/incident"

export function listIncidents(params: ListIncidentsParams, signal?: AbortSignal) {
  return http.get<PagedResult<IncidentSummary>>("/api/v1/incidents", { query: params, signal })
}