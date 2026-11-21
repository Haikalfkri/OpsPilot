import { useMutation, useQueryClient } from "@tanstack/react-query"
import { markAllNotificationsRead, markNotificationRead } from "@/api/notifications"
import { notificationKeys } from "@/api/query-keys"

export function useMarkNotificationRead() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: markNotificationRead,
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all })
    },
  })
}

export function useMarkAllNotificationsRead() {
    const queryClient = useQueryClient()

    return useMutation({
        mutationFn: markAllNotificationsRead,
        onSettled: () => {
            void queryClient.invalidateQueries({ queryKey: notificationKeys.all })
        },
    })
}