import { describe, expect, it } from "vitest"
import { canManageIncidents, canManageUsers } from "@/lib/permissions"
import type { UserRole } from "@/types/auth"

describe("permissions (cermin kebijakan backend)", () => {
  const incidentCases: [UserRole, boolean][] = [
    ["Admin", true],
    ["Responder", true],
    ["Viewer", false],
  ]

  it.each(incidentCases)("canManageIncidents(%s) = %s", (role, expected) => {
    expect(canManageIncidents(role)).toBe(expected)
  })

  const userCases: [UserRole, boolean][] = [
    ["Admin", true],
    ["Responder", false],
    ["Viewer", false],
  ]

  it.each(userCases)("canManageUsers(%s) = %s", (role, expected) => {
    expect(canManageUsers(role)).toBe(expected)
  })
})