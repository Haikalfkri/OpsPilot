import type { ListIncidentsParams } from "@/types/incident"

export const incidentKeys = {
    all: ["incidents"] as const,
    list: (params: ListIncidentsParams) => [...incidentKeys.all, "list", params] as const,
}

export const teamKeys = {
    members: ["team", "members"] as const,
}