import { keepPreviousData, useQuery } from "@tanstack/react-query"
import { getUnreadCount, listNotifications } from "@/api/notifications"
import { notificationKeys } from "@/api/query-keys"
import { NOTIFICATION_POLL_MS } from "@/lib/constants"
import type { ListNotificationsParams } from "@/types/notification"

export function useNotifications(params: ListNotificationsParams) {
  return useQuery({
    queryKey: notificationKeys.list(params),
    queryFn: ({ signal }) => listNotifications(params, signal),
    placeholderData: keepPreviousData,
    refetchInterval: NOTIFICATION_POLL_MS,
  })
}

export function useUnreadCount() {
  return useQuery({
    queryKey: notificationKeys.unreadCount(),
    queryFn: ({ signal }) => getUnreadCount(signal),
    select: (data) => data.count,

    refetchInterval: NOTIFICATION_POLL_MS,
    refetchOnWindowFocus: true,
  })
}