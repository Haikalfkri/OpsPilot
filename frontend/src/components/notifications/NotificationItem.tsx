import { Link } from "react-router"
import { getErrorMessage } from "@/api/errors"
import { Badge } from "@/components/ui/badge"
import { Button, buttonVariants } from "@/components/ui/button"
import { useMarkNotificationRead } from "@/hooks/useNotificationMutations"
import { formatDateTime, formatRelative } from "@/lib/format"
import { NOTIFICATION_LABELS, isBreachNotification } from "@/lib/notifications"
import { cn } from "@/lib/utils"
import type { AppNotification } from "@/types/notification"

export function NotificationItem({ notification }: { notification: AppNotification }) {
  const markRead = useMarkNotificationRead()
  const unread = !notification.isRead

  return (
    <li
      className={cn(
        "flex flex-col gap-3 rounded-md border p-4 sm:flex-row sm:items-start sm:justify-between",
        unread && "bg-muted/50",
      )}
    >
      <div className="min-w-0 space-y-1">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant={isBreachNotification(notification.type) ? "destructive" : "outline"}>
            {NOTIFICATION_LABELS[notification.type]}
          </Badge>
          {unread && <Badge variant="secondary">New</Badge>}
        </div>

        <p className={cn("text-sm", unread && "font-medium")}>{notification.title}</p>
        <p className="break-words text-sm text-muted-foreground">{notification.message}</p>
        <p className="text-xs text-muted-foreground" title={formatDateTime(notification.createdAtUtc)}>
          {formatRelative(notification.createdAtUtc)}
        </p>

        {markRead.isError && (
          <p role="alert" className="text-xs text-destructive">
            {getErrorMessage(markRead.error)}
          </p>
        )}
      </div>

      <div className="flex shrink-0 gap-2">
        {notification.incidentId && (
          <Link
            to={`/incidents/${notification.incidentId}`}
            className={buttonVariants({ variant: "outline", size: "sm" })}
            onClick={() => {
              if (unread) markRead.mutate(notification.id)
            }}
          >
            View incident
          </Link>
        )}

        {unread && (
          <Button variant="ghost" size="sm" disabled={markRead.isPending} onClick={() => markRead.mutate(notification.id)}>
            Mark as read
          </Button>
        )}
      </div>
    </li>
  )
}