import { useMutation, useQueryClient, type QueryClient } from "@tanstack/react-query"
import { addComment, assignIncident, changeIncidentStatus, updateIncident, createIncident } from "@/api/incidents"
import { dashboardKeys, incidentKeys } from "@/api/query-keys"
import type { IncidentDetail, IncidentStatus, UpdateIncidentRequest, CreateIncidentRequest, } from "@/types/incident"

function syncIncident(queryClient: QueryClient, id: string, incident: IncidentDetail) {
  queryClient.setQueryData(incidentKeys.detail(id), incident)
  void queryClient.invalidateQueries({ queryKey: incidentKeys.lists() })
  void queryClient.invalidateQueries({ queryKey: incidentKeys.activitiesAll(id) })
  void queryClient.invalidateQueries({ queryKey: dashboardKeys.all })
}

function refreshDetail(queryClient: QueryClient, id: string) {
  void queryClient.invalidateQueries({ queryKey: incidentKeys.detail(id), exact: true })
}

export function useUpdateIncident(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: UpdateIncidentRequest) => updateIncident(id, body),
    onSuccess: (incident) => syncIncident(queryClient, id, incident),
    onError: () => refreshDetail(queryClient, id),
  })
}

export function useChangeStatus(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (status: IncidentStatus) => changeIncidentStatus(id, status),
    onSuccess: (incident) => syncIncident(queryClient, id, incident),
    onError: () => refreshDetail(queryClient, id),
  })
}

export function useAssignIncident(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (assigneeId: string | null) => assignIncident(id, assigneeId),
    onSuccess: (incident) => syncIncident(queryClient, id, incident),
    onError: () => refreshDetail(queryClient, id),
  })
}

export function useAddComment(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: string) => addComment(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: incidentKeys.commentsAll(id) })
      void queryClient.invalidateQueries({ queryKey: incidentKeys.activitiesAll(id) })
    },
  })
}

export function useCreateIncident() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: CreateIncidentRequest) => createIncident(body),
    onSuccess: (incident) => {
      queryClient.setQueryData(incidentKeys.detail(incident.id), incident)
      void queryClient.invalidateQueries({ queryKey: incidentKeys.lists() })
      void queryClient.invalidateQueries({ queryKey: dashboardKeys.all })
    },
  })
}