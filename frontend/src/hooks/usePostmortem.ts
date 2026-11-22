import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { getPostmortem, savePostmortem } from "@/api/postmortem"
import { incidentKeys } from "@/api/query-keys"
import type { SavePostmortemRequest } from "@/types/postmortem"

// enabled: baru bertanya ke backend kalau incident sudah Resolved atau Closed
export function usePostmortem(incidentId: string, enabled: boolean) {
  return useQuery({
    queryKey: incidentKeys.postmortem(incidentId),
    queryFn: ({ signal }) => getPostmortem(incidentId, signal),
    enabled,
  })
}

export function useSavePostmortem(incidentId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: SavePostmortemRequest) => savePostmortem(incidentId, body),

    onSuccess: (postmortem) => {
      // Backend menjawab dengan postmortem terbaru: langsung dipasang ke cache
      queryClient.setQueryData(incidentKeys.postmortem(incidentId), postmortem)

      // Timeline mendapat entri baru ("Postmortem created" atau "Postmortem updated")
      void queryClient.invalidateQueries({ queryKey: incidentKeys.activitiesAll(incidentId) })
    },

    // Ditolak (mis. status incident sudah berubah, atau sudah ditulis orang lain): ambil ulang keduanya
    onError: () => {
      void queryClient.invalidateQueries({ queryKey: incidentKeys.postmortem(incidentId) })
      void queryClient.invalidateQueries({ queryKey: incidentKeys.detail(incidentId), exact: true })
    },
  })
}