import { keepPreviousData, useQuery } from "@tanstack/react-query"
import { listIncidents } from "@/api/incidents"
import { incidentKeys } from "@/api/query-keys"
import type { ListIncidentsParams } from "@/types/incident"


export function useIncidents(params: ListIncidentsParams) {
    return useQuery({
        queryKey: incidentKeys.list(params),
        queryFn: ({ signal }) => listIncidents(params, signal),
        placeholderData: keepPreviousData,
    })
}