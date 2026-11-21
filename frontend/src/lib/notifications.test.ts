import { describe, expect, it } from "vitest"
import { NOTIFICATION_LABELS, formatUnreadBadge, isBreachNotification } from "@/lib/notifications"
import type { NotificationType } from "@/types/notification"

describe("formatUnreadBadge", () => {
  it.each([
    [0, ""],
    [-3, ""],
    [1, "1"],
    [9, "9"],
    [99, "99"],
    [100, "99+"],
    [2500, "99+"],
  ])("%s unread shows '%s'", (count, expected) => {
    expect(formatUnreadBadge(count)).toBe(expected)
  })
})

describe("notification types", () => {
  const types: NotificationType[] = ["IncidentAssigned", "ResponseSlaBreached", "ResolutionSlaBreached"]

  it.each(types)("%s has a label", (type) => {
    expect(NOTIFICATION_LABELS[type]).toBeTruthy()
  })

  it("flags only the SLA breaches as urgent", () => {
    expect(isBreachNotification("IncidentAssigned")).toBe(false)
    expect(isBreachNotification("ResponseSlaBreached")).toBe(true)
    expect(isBreachNotification("ResolutionSlaBreached")).toBe(true)
  })
})