import { http } from "@/api/http"
import type { PagedResult } from "@/types/api"
import type {
  AppNotification,
  ListNotificationsParams,
  MarkAllReadResult,
  UnreadCount,
} from "@/types/notification"

export function listNotifications(params: ListNotificationsParams, signal?: AbortSignal) {
  return http.get<PagedResult<AppNotification>>("/api/v1/notifications", { query: params, signal })
}

export function getUnreadCount(signal?: AbortSignal) {
  return http.get<UnreadCount>("/api/v1/notifications/unread-count", { signal })
}

export function markNotificationRead(id: string) {
  return http.patch<AppNotification>(`/api/v1/notifications/${encodeURIComponent(id)}/read`)
}

export function markAllNotificationsRead() {
  return http.post<MarkAllReadResult>("/api/v1/notifications/read-all")
}