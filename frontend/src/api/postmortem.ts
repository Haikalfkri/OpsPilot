import { isApiError } from "@/api/errors"
import { http } from "@/api/http"
import type { Postmortem, SavePostmortemRequest } from "@/types/postmortem"

const url = (incidentId: string) => `/api/v1/incidents/${encodeURIComponent(incidentId)}/postmortem`

export async function getPostmortem(incidentId: string, signal?: AbortSignal): Promise<Postmortem | null> {
  try {
    return await http.get<Postmortem>(url(incidentId), { signal })
  } catch (error) {
    if (isApiError(error) && error.status === 404) return null
    throw error
  }
}

export function savePostmortem(incidentId: string, body: SavePostmortemRequest) {
  return http.put<Postmortem>(url(incidentId), body)
}