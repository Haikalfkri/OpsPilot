import { render, screen } from "@testing-library/react"
import { createMemoryRouter, RouterProvider } from "react-router"
import { describe, expect, it, vi } from "vitest"
import { RequireRole } from "@/components/auth/RequireRole"
import { AuthContext, type AuthContextValue } from "@/hooks/auth-context"
import { canManageIncidents } from "@/lib/permissions"
import type { UserRole } from "@/types/auth"

function renderAs(role: UserRole) {
  const auth: AuthContextValue = {
    user: {
      id: "user-1",
      displayName: `Demo ${role}`,
      email: `${role.toLowerCase()}@opspilot.local`,
      role,
      teamId: "team-1",
      isActive: true,
    },
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
  }
  const router = createMemoryRouter(
    [{ element: <RequireRole allow={canManageIncidents} />, children: [{ path: "/new", element: <p>inside the form</p> }] }],
    { initialEntries: ["/new"] },
  )

  render(
    <AuthContext.Provider value={auth}>
      <RouterProvider router={router} />
    </AuthContext.Provider>,
  )
}

describe("RequireRole with canManageIncidents", () => {
  it.each(["Admin", "Responder"] as const)("lets a %s in", async (role) => {
    renderAs(role)

    expect(await screen.findByText("inside the form")).toBeTruthy()
  })

  it("keeps a Viewer out", async () => {
    renderAs("Viewer")

    expect(await screen.findByText("You do not have access to this page.")).toBeTruthy()
    expect(screen.queryByText("inside the form")).toBeNull()
  })
})