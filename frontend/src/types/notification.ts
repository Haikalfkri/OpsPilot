export type NotificationType = "IncidentAssigned" | "ResponseSlaBreached" | "ResolutionSlaBreached"

export interface AppNotification {
  id: string
  type: NotificationType
  title: string
  message: string
  incidentId: string | null
  isRead: boolean
  createdAtUtc: string
  readAtUtc: string | null
}

export interface UnreadCount {
  count: number
}

export interface MarkAllReadResult {
  markedCount: number
}

export type ListNotificationsParams = {
  page: number
  pageSize: number
  isRead?: boolean
}