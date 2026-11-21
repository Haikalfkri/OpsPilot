import { keepPreviousData, useQuery } from "@tanstack/react-query"
import { getIncident, listActivities, listComments } from "@/api/incidents"
import { incidentKeys } from "@/api/query-keys"

export function useIncident(id: string) {
    return useQuery({
        queryKey: incidentKeys.detail(id),
        queryFn: ({ signal }) => getIncident(id, signal),
    })
}

export function useIncidentActivities(id: string, page: number) {
  return useQuery({
    queryKey: incidentKeys.activities(id, page),
    queryFn: ({ signal }) => listActivities(id, page, signal),
    placeholderData: keepPreviousData,
  })
}

export function useIncidentComments(id: string, page: number) {
  return useQuery({
    queryKey: incidentKeys.comments(id, page),
    queryFn: ({ signal }) => listComments(id, page, signal),
    placeholderData: keepPreviousData,
  })
}