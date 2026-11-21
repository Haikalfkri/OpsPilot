import { fireEvent, render, screen } from "@testing-library/react"
import { createMemoryRouter, RouterProvider } from "react-router"
import { describe, expect, it, vi } from "vitest"
import { AuthContext, type AuthContextValue } from "@/hooks/auth-context"
import { routes } from "@/routes"
import type { User, UserRole } from "@/types/auth"
import { QueryClient, QueryClientProvider } from "@tanstack/react-query"

vi.mock("@/api/notifications", () => ({
  getUnreadCount: vi.fn().mockResolvedValue({ count: 0 }),
  listNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
  markAllNotificationsRead: vi.fn(),
}))

function makeUser(role: UserRole): User {
  return {
    id: "user-1",
    displayName: `Demo ${role}`,
    email: `${role.toLowerCase()}@opspilot.local`,
    role,
    teamId: "team-1",
    isActive: true,
  }
}

function renderAt(path: string, user: User | null) {
  const auth: AuthContextValue = {
    user,
    isAuthenticated: user !== null,
    login: vi.fn(),
    logout: vi.fn(),
  }
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })

  render(
    <QueryClientProvider client={client}>
      <AuthContext.Provider value={auth}>
        <RouterProvider router={router} />
      </AuthContext.Provider>
    </QueryClientProvider>,
  )

  return { router, auth }
}

describe("route guards", () => {
  it("sends a signed-out visitor to the login page", async () => {
    const { router } = renderAt("/incidents", null)

    expect(await screen.findByText("Sign in to OpsPilot")).toBeTruthy()
    expect(router.state.location.pathname).toBe("/login")
  })

  it("sends a signed-out visitor to login even for an unknown address", async () => {
    const { router } = renderAt("/asal-asalan", null)

    expect(await screen.findByText("Sign in to OpsPilot")).toBeTruthy()
    expect(router.state.location.pathname).toBe("/login")
  })

  it("moves a signed-in user away from the login page", async () => {
    const { router } = renderAt("/login", makeUser("Admin"))

    expect(await screen.findByText(/Welcome back, Demo Admin/)).toBeTruthy()
    expect(router.state.location.pathname).toBe("/")
  })

  it("shows a Viewer the no-access page on /users, without the Users menu", async () => {
    renderAt("/users", makeUser("Viewer"))

    expect(await screen.findByText("You do not have access to this page.")).toBeTruthy()
    expect(screen.queryAllByRole("link", { name: "Users" })).toHaveLength(0)
  })

  it("lets an Admin open /users and see the Users menu", async () => {
    renderAt("/users", makeUser("Admin"))

    expect(await screen.findByRole("heading", { name: "Users" })).toBeTruthy()
    expect(screen.getAllByRole("link", { name: "Users" }).length).toBeGreaterThan(0)
  })

  it("hides the Users menu from a Responder but keeps Incidents", async () => {
    renderAt("/", makeUser("Responder"))

    // Menu digambar dua kali (samping dan mendatar), jadi memakai getAll/queryAll
    expect((await screen.findAllByRole("link", { name: "Incidents" })).length).toBeGreaterThan(0)
    expect(screen.queryAllByRole("link", { name: "Users" })).toHaveLength(0)
  })

  it("shows the 404 page inside the layout for a signed-in user", async () => {
    renderAt("/asal-asalan", makeUser("Admin"))

    expect(await screen.findByText("Page not found")).toBeTruthy()
    expect(screen.getAllByRole("link", { name: "Dashboard" }).length).toBeGreaterThan(0)
  })

  it("calls logout when Sign out is clicked", async () => {
    const { auth } = renderAt("/", makeUser("Admin"))

    fireEvent.click(await screen.findByRole("button", { name: "Sign out" }))

    expect(auth.logout).toHaveBeenCalledTimes(1)
  })
})