import type { ReactNode } from "react"
import { useSearchParams } from "react-router"
import { getErrorMessage } from "@/api/errors"
import { NotificationItem } from "@/components/notifications/NotificationItem"
import { Pagination } from "@/components/Pagination"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { useMarkAllNotificationsRead } from "@/hooks/useNotificationMutations"
import { useNotifications, useUnreadCount } from "@/hooks/useNotifications"
import { NOTIFICATIONS_PAGE_SIZE } from "@/lib/constants"
import {
  parseNotificationFilters,
  toNotificationApiParams,
  toNotificationSearchParams,
  type NotificationFilters,
} from "@/lib/notification-filters"

export default function NotificationsPage() {
  const [searchParams, setSearchParams] = useSearchParams()

  const filters = parseNotificationFilters(searchParams)
  const notifications = useNotifications(toNotificationApiParams(filters))
  const unreadCount = useUnreadCount()
  const markAll = useMarkAllNotificationsRead()

  const unread = unreadCount.data ?? 0

  function update(patch: Partial<NotificationFilters>) {
    setSearchParams(toNotificationSearchParams({ ...filters, page: 1, ...patch }))
  }

  let content: ReactNode

  if (notifications.isPending) {
    content = (
      <div className="space-y-3" aria-busy="true" aria-label="Loading notifications">
        {Array.from({ length: 4 }, (_, index) => (
          <Skeleton key={index} className="h-20 w-full" />
        ))}
      </div>
    )
  } else if (notifications.isError) {
    content = (
      <Alert variant="destructive">
        <AlertTitle>Could not load notifications</AlertTitle>
        <AlertDescription className="space-y-3">
          <p>{getErrorMessage(notifications.error)}</p>
          <Button variant="outline" size="sm" onClick={() => notifications.refetch()}>
            Try again
          </Button>
        </AlertDescription>
      </Alert>
    )
  } else if (notifications.data.items.length === 0) {
    content = (
      <div className="rounded-md border p-8 text-center">
        {notifications.data.totalCount > 0 ? (
          <div className="space-y-3">
            <p className="font-medium">This page does not exist.</p>
            <Button variant="outline" onClick={() => update({ page: 1 })}>
              Go to the first page
            </Button>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            {filters.filter === "unread" ? "You are all caught up." : "No notifications yet."}
          </p>
        )}
      </div>
    )
  } else {
    content = (
      <>
        <ul className={notifications.isPlaceholderData ? "space-y-3 opacity-60" : "space-y-3"}>
          {notifications.data.items.map((notification) => (
            <NotificationItem key={notification.id} notification={notification} />
          ))}
        </ul>

        <Pagination
          page={notifications.data.page}
          totalPages={notifications.data.totalPages}
          totalCount={notifications.data.totalCount}
          pageSize={NOTIFICATIONS_PAGE_SIZE}
          disabled={notifications.isPlaceholderData}
          onPageChange={(page) => update({ page })}
        />
      </>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Notifications</h1>

        {unread > 0 && (
          <Button variant="outline" size="sm" disabled={markAll.isPending} onClick={() => markAll.mutate()}>
            {markAll.isPending ? "Saving…" : "Mark all as read"}
          </Button>
        )}
      </div>

      {markAll.isError && (
        <Alert variant="destructive">
          <AlertDescription>{getErrorMessage(markAll.error)}</AlertDescription>
        </Alert>
      )}

      <div role="group" aria-label="Filter notifications" className="flex gap-2">
        <Button
          size="sm"
          variant={filters.filter === "all" ? "default" : "outline"}
          aria-pressed={filters.filter === "all"}
          onClick={() => update({ filter: "all" })}
        >
          All
        </Button>
        <Button
          size="sm"
          variant={filters.filter === "unread" ? "default" : "outline"}
          aria-pressed={filters.filter === "unread"}
          onClick={() => update({ filter: "unread" })}
        >
          {unread > 0 ? `Unread (${unread})` : "Unread"}
        </Button>
      </div>

      {content}
    </div>
  )
}