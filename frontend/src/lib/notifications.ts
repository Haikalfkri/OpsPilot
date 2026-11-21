import type { NotificationType } from "@/types/notification"

export function formatUnreadBadge(count: number): string {
  if (count <= 0) return ""

  return count > 99 ? "99+" : String(count)
}

export const NOTIFICATION_LABELS: Record<NotificationType, string> = {
  IncidentAssigned: "Assigned",
  ResponseSlaBreached: "SLA response",
  ResolutionSlaBreached: "SLA resolution",
}

export function isBreachNotification(type: NotificationType): boolean {
  return type === "ResponseSlaBreached" || type === "ResolutionSlaBreached"
}