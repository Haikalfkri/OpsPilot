import { NOTIFICATIONS_PAGE_SIZE } from "@/lib/constants"
import type { ListNotificationsParams } from "@/types/notification"

export type NotificationFilter = "all" | "unread"

export interface NotificationFilters {
  filter: NotificationFilter
  page: number
}

// URL -> filter. Nilai yang tidak dikenal jatuh ke bawaan (semua, halaman 1).
export function parseNotificationFilters(params: URLSearchParams): NotificationFilters {
  const page = Number.parseInt(params.get("page") ?? "", 10)

  return {
    filter: params.get("filter") === "unread" ? "unread" : "all",
    page: Number.isInteger(page) && page >= 1 ? page : 1,
  }
}

// Filter -> URL. Nilai bawaan tidak ditulis, supaya URL tetap pendek.
export function toNotificationSearchParams(filters: NotificationFilters): URLSearchParams {
  const params = new URLSearchParams()

  if (filters.filter === "unread") params.set("filter", "unread")
  if (filters.page > 1) params.set("page", String(filters.page))

  return params
}

// Filter -> parameter untuk backend. "all" berarti tidak mengirim isRead sama sekali.
export function toNotificationApiParams(filters: NotificationFilters): ListNotificationsParams {
  return {
    page: filters.page,
    pageSize: NOTIFICATIONS_PAGE_SIZE,
    isRead: filters.filter === "unread" ? false : undefined,
  }
}