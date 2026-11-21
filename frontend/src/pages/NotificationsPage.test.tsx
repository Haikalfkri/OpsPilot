import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { createMemoryRouter, RouterProvider } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/errors"
import {
  getUnreadCount,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
} from "@/api/notifications"
import NotificationsPage from "@/pages/NotificationsPage"
import type { PagedResult } from "@/types/api"
import type { AppNotification } from "@/types/notification"

vi.mock("@/api/notifications", () => ({
  listNotifications: vi.fn(),
  getUnreadCount: vi.fn(),
  markNotificationRead: vi.fn(),
  markAllNotificationsRead: vi.fn(),
}))

const INCIDENT_ID = "33333333-3333-3333-3333-333333333333"

function makeNotification(overrides: Partial<AppNotification> = {}): AppNotification {
  return {
    id: "n1",
    type: "IncidentAssigned",
    title: "Incident assigned to you",
    message: 'Demo Admin assigned you to "Database is down" (Critical).',
    incidentId: INCIDENT_ID,
    isRead: false,
    createdAtUtc: "2026-10-30T08:00:00Z",
    readAtUtc: null,
    ...overrides,
  }
}

function pageOf<T>(items: T[], overrides: Partial<PagedResult<T>> = {}): PagedResult<T> {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1, ...overrides }
}

function renderPage(path = "/notifications") {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [
      { path: "/notifications", element: <NotificationsPage /> },
      { path: "/incidents/:id", element: <p>incident page</p> },
    ],
    { initialEntries: [path] },
  )

  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )

  return router
}

const lastParams = () => vi.mocked(listNotifications).mock.lastCall![0]

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(listNotifications).mockResolvedValue(pageOf([makeNotification()]))
  vi.mocked(getUnreadCount).mockResolvedValue({ count: 1 })
  vi.mocked(markNotificationRead).mockResolvedValue(makeNotification({ isRead: true }))
  vi.mocked(markAllNotificationsRead).mockResolvedValue({ markedCount: 1 })
})

describe("NotificationsPage: reading", () => {
  it("shows an unread notification with its label, a New marker, and its message", async () => {
    renderPage()

    expect(await screen.findByText("Incident assigned to you")).toBeTruthy()
    expect(screen.getByText("Assigned")).toBeTruthy()
    expect(screen.getByText("New")).toBeTruthy()
    expect(screen.getByText(/Demo Admin assigned you to/)).toBeTruthy()
  })

  it("shows no New marker and no Mark as read button for a read notification", async () => {
    vi.mocked(listNotifications).mockResolvedValue(pageOf([makeNotification({ isRead: true })]))
    renderPage()

    await screen.findByText("Incident assigned to you")

    expect(screen.queryByText("New")).toBeNull()
    expect(screen.queryByRole("button", { name: "Mark as read" })).toBeNull()
  })

  it("labels an SLA breach clearly", async () => {
    vi.mocked(listNotifications).mockResolvedValue(
      pageOf([makeNotification({ type: "ResponseSlaBreached", title: "SLA response target breached" })]),
    )
    renderPage()

    expect(await screen.findByText("SLA response target breached")).toBeTruthy()
    expect(screen.getByText("SLA response")).toBeTruthy()
  })

  it("says so when there are no notifications at all", async () => {
    vi.mocked(listNotifications).mockResolvedValue(pageOf<AppNotification>([]))
    renderPage()

    expect(await screen.findByText("No notifications yet.")).toBeTruthy()
  })

  it("says you are caught up when the unread list is empty", async () => {
    vi.mocked(listNotifications).mockResolvedValue(pageOf<AppNotification>([]))
    renderPage("/notifications?filter=unread")

    expect(await screen.findByText("You are all caught up.")).toBeTruthy()
  })

  it("shows the error and recovers when Try again is clicked", async () => {
    vi.mocked(listNotifications)
      .mockRejectedValueOnce(new ApiError({ status: 500, title: "Server exploded" }))
      .mockResolvedValue(pageOf([makeNotification()]))
    renderPage()

    expect(await screen.findByText("Server exploded")).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Try again" }))

    expect(await screen.findByText("Incident assigned to you")).toBeTruthy()
  })
})

describe("NotificationsPage: filter and paging", () => {
  it("asks for everything by default", async () => {
    renderPage()

    await waitFor(() => expect(listNotifications).toHaveBeenCalled())

    expect(lastParams().isRead).toBeUndefined()
    expect(lastParams()).toMatchObject({ page: 1, pageSize: 20 })
  })

  it("asks only for unread ones when the URL says so", async () => {
    renderPage("/notifications?filter=unread")

    await waitFor(() => expect(listNotifications).toHaveBeenCalled())

    expect(lastParams().isRead).toBe(false)
  })

  it("switches to unread, and puts the filter in the URL", async () => {
    const router = renderPage()
    await screen.findByText("Incident assigned to you")

    fireEvent.click(screen.getByRole("button", { name: /^Unread/ }))

    await waitFor(() => expect(lastParams().isRead).toBe(false))
    expect(router.state.location.search).toBe("?filter=unread")
  })

  it("shows the unread number on the Unread button", async () => {
    vi.mocked(getUnreadCount).mockResolvedValue({ count: 4 })
    renderPage()

    expect(await screen.findByRole("button", { name: "Unread (4)" })).toBeTruthy()
  })

  it("asks for the next page", async () => {
    vi.mocked(listNotifications).mockResolvedValue(pageOf([makeNotification()], { totalCount: 45, totalPages: 3 }))
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Next" }))

    await waitFor(() => expect(lastParams().page).toBe(2))
  })
})

describe("NotificationsPage: marking as read", () => {
  it("marks one notification as read, then reloads both the list and the count", async () => {
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Mark as read" }))

    await waitFor(() => expect(markNotificationRead).toHaveBeenCalledWith("n1"))

    // Cache disegarkan: daftar dan angka diambil ulang (lonceng akan ikut turun)
    await waitFor(() => expect(listNotifications).toHaveBeenCalledTimes(2))
    await waitFor(() => expect(getUnreadCount).toHaveBeenCalledTimes(2))
  })

  it("marks the notification as read when its incident is opened", async () => {
    renderPage()

    const link = await screen.findByRole("link", { name: "View incident" })
    expect(link.getAttribute("href")).toBe(`/incidents/${INCIDENT_ID}`)

    fireEvent.click(link)

    expect(await screen.findByText("incident page")).toBeTruthy()
    await waitFor(() => expect(markNotificationRead).toHaveBeenCalledWith("n1"))
  })

  it("does not call the API again when opening the incident of an already-read notification", async () => {
    vi.mocked(listNotifications).mockResolvedValue(pageOf([makeNotification({ isRead: true })]))
    renderPage()

    fireEvent.click(await screen.findByRole("link", { name: "View incident" }))

    expect(await screen.findByText("incident page")).toBeTruthy()
    expect(markNotificationRead).not.toHaveBeenCalled()
  })

  it("shows the reason next to the notification when marking fails", async () => {
    vi.mocked(markNotificationRead).mockRejectedValue(
      new ApiError({ status: 404, title: "Not found", detail: "Notification not found." }),
    )
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Mark as read" }))

    expect(await screen.findByText("Notification not found.")).toBeTruthy()
  })

  it("offers Mark all as read only when something is unread, and marks everything", async () => {
    vi.mocked(getUnreadCount).mockResolvedValue({ count: 3 })
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Mark all as read" }))

    await waitFor(() => expect(markAllNotificationsRead).toHaveBeenCalledTimes(1))
  })

  it("hides Mark all as read when everything is already read", async () => {
    vi.mocked(getUnreadCount).mockResolvedValue({ count: 0 })
    vi.mocked(listNotifications).mockResolvedValue(pageOf([makeNotification({ isRead: true })]))
    renderPage()

    await screen.findByText("Incident assigned to you")
    await waitFor(() => expect(getUnreadCount).toHaveBeenCalled())

    expect(screen.queryByRole("button", { name: "Mark all as read" })).toBeNull()
  })
})