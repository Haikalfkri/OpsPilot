import type { ListIncidentsParams } from "@/types/incident"

const all = ["incidents"] as const

export const incidentKeys = {
  all,
  lists: () => [...all, "list"] as const,
  list: (params: ListIncidentsParams) => [...all, "list", params] as const,
  detail: (id: string) => [...all, "detail", id] as const,
  activitiesAll: (id: string) => [...all, "detail", id, "activities"] as const,
  activities: (id: string, page: number) => [...all, "detail", id, "activities", page] as const,
  commentsAll: (id: string) => [...all, "detail", id, "comments"] as const,
  comments: (id: string, page: number) => [...all, "detail", id, "comments", page] as const,
}

export const teamKeys = {
  members: ["team", "members"] as const,
}