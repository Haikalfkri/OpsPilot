import { describe, expect, it } from "vitest"
import { getNavItems } from "@/components/layout/navigation"

const labels = (role: Parameters<typeof getNavItems>[0]) => getNavItems(role).map((item) => item.label)

describe("getNavItems", () => {
  it("shows every item to an Admin", () => {
    expect(labels("Admin")).toEqual(["Dashboard", "Incidents", "Notifications", "Users"])
  })

  it.each(["Responder", "Viewer"] as const)("hides Users from a %s", (role) => {
    expect(labels(role)).toEqual(["Dashboard", "Incidents", "Notifications"])
  })
})