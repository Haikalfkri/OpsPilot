import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { render, screen, waitFor } from "@testing-library/react"
import { createMemoryRouter, RouterProvider } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/errors"
import { getUnreadCount } from "@/api/notifications"
import { NotificationBell } from "@/components/layout/NotificationBell"

vi.mock("@/api/notifications", () => ({ getUnreadCount: vi.fn() }))

function renderBell() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: "/", element: <NotificationBell /> }])

  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  vi.resetAllMocks()
})

describe("NotificationBell", () => {
  it("links to the notifications page", async () => {
    vi.mocked(getUnreadCount).mockResolvedValue({ count: 0 })
    renderBell()

    const link = await screen.findByRole("link", { name: "Notifications" })

    expect(link.getAttribute("href")).toBe("/notifications")
  })

  it("shows the number, and says it in the accessible name", async () => {
    vi.mocked(getUnreadCount).mockResolvedValue({ count: 3 })
    renderBell()

    expect(await screen.findByRole("link", { name: "Notifications, 3 unread" })).toBeTruthy()
    expect(screen.getByText("3")).toBeTruthy()
  })

  it("caps a large number at 99+", async () => {
    vi.mocked(getUnreadCount).mockResolvedValue({ count: 250 })
    renderBell()

    expect(await screen.findByText("99+")).toBeTruthy()
  })

  it("shows no number when everything is read", async () => {
    vi.mocked(getUnreadCount).mockResolvedValue({ count: 0 })
    renderBell()

    await waitFor(() => expect(getUnreadCount).toHaveBeenCalled())

    expect(screen.queryByText("0")).toBeNull()
    expect(screen.getByRole("link", { name: "Notifications" })).toBeTruthy()
  })

  it("stays quiet when the count cannot be loaded", async () => {
    vi.mocked(getUnreadCount).mockRejectedValue(new ApiError({ status: 0, title: "Cannot reach the server" }))
    renderBell()

    await waitFor(() => expect(getUnreadCount).toHaveBeenCalled())

    expect(screen.queryByRole("alert")).toBeNull()
    expect(screen.queryByText("Cannot reach the server")).toBeNull()
    expect(screen.getByRole("link", { name: "Notifications" })).toBeTruthy()
  })
})