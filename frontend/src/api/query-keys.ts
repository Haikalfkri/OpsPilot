import type { ListIncidentsParams } from "@/types/incident"
import type { ListNotificationsParams } from "@/types/notification"

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

export const slaKeys = {
  policy: ["sla", "policy"] as const,
}

const notificationsRoot = ["notifications"] as const

export const notificationKeys = {
  all: notificationsRoot,
  lists: () => [...notificationsRoot, "list"] as const,
  list: (params: ListNotificationsParams) => [...notificationsRoot, "list", params] as const,
  unreadCount: () => [...notificationsRoot, "unread-count"] as const,
}

const dashboardRoot = ["dashboard"] as const

export const dashboardKeys = {
  all: dashboardRoot,
  summary: (days: number) => [...dashboardRoot, "summary", days] as const,
  breakdown: (days: number) => [...dashboardRoot, "breakdown", days] as const,
  trends: (days: number) => [...dashboardRoot, "trends", days] as const,
}