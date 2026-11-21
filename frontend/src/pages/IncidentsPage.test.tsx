import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react"
import { createMemoryRouter, RouterProvider } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/errors"
import { listIncidents } from "@/api/incidents"
import { listTeamMembers } from "@/api/team"
import { AuthContext, type AuthContextValue } from "@/hooks/auth-context"
import IncidentsPage from "@/pages/IncidentsPage"
import type { PagedResult } from "@/types/api"
import type { UserRole } from "@/types/auth"
import type { IncidentSummary } from "@/types/incident"
import type { TeamMember } from "@/types/team"

// Mengganti dua modul API dengan fungsi palsu: tes tidak menyentuh jaringan sama sekali
vi.mock("@/api/incidents", () => ({ listIncidents: vi.fn() }))
vi.mock("@/api/team", () => ({ listTeamMembers: vi.fn() }))

const INCIDENT_ID = "11111111-1111-1111-1111-111111111111"

function makeIncident(overrides: Partial<IncidentSummary> = {}): IncidentSummary {
  return {
    id: INCIDENT_ID,
    title: "Database is down",
    severity: "Critical",
    status: "Open",
    assigneeId: null,
    assigneeName: null,
    createdAtUtc: "2026-10-30T08:00:00Z",
    updatedAtUtc: "2026-10-30T08:00:00Z",
    firstRespondedAtUtc: null,
    resolvedAtUtc: null,
    responseDueAtUtc: "2026-10-30T08:15:00Z",
    resolutionDueAtUtc: "2026-10-30T12:00:00Z",
    responseSla: "Breached",
    resolutionSla: "Pending",
    ...overrides,
  }
}

function pageOf<T>(items: T[], overrides: Partial<PagedResult<T>> = {}): PagedResult<T> {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1, ...overrides }
}

function renderPage(path: string, role: UserRole = "Responder") {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
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
  const router = createMemoryRouter([{ path: "/incidents", element: <IncidentsPage /> }], {
    initialEntries: [path],
  })

  render(
    <QueryClientProvider client={client}>
      <AuthContext.Provider value={auth}>
        <RouterProvider router={router} />
      </AuthContext.Provider>
    </QueryClientProvider>,
  )
}

// Parameter yang dikirim pada pemanggilan API terakhir
const lastParams = () => vi.mocked(listIncidents).mock.lastCall![0]

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(listIncidents).mockResolvedValue(pageOf<IncidentSummary>([]))
  vi.mocked(listTeamMembers).mockResolvedValue(
    pageOf<TeamMember>([{ id: "a0000000-0000-0000-0000-000000000002", displayName: "Demo Responder", role: "Responder" }]),
  )
})

describe("IncidentsPage", () => {
  it("shows each incident with its badges and a link to its detail page", async () => {
    vi.mocked(listIncidents).mockResolvedValue(
      pageOf([makeIncident({ status: "Investigating", assigneeName: "Demo Responder" })]),
    )
    renderPage("/incidents")

    const link = await screen.findByRole("link", { name: "Database is down" })
    expect(link.getAttribute("href")).toBe(`/incidents/${INCIDENT_ID}`)

    const row = within(link.closest("tr")!)
    expect(row.getByText("Critical")).toBeTruthy()
    expect(row.getByText("Investigating")).toBeTruthy()
    expect(row.getByText("Breached")).toBeTruthy()
    expect(row.getByText("Demo Responder")).toBeTruthy()
  })

  it("sends the filters from the URL to the API", async () => {
    renderPage("/incidents?status=Open&severity=High&q=disk&sort=due&page=2")

    await waitFor(() => expect(listIncidents).toHaveBeenCalled())

    expect(lastParams()).toMatchObject({
      status: "Open",
      severity: "High",
      search: "disk",
      sortBy: "dueAt",
      sortDirection: "asc",
      page: 2,
      pageSize: 20,
    })
  })

  it("ignores invalid values in the URL", async () => {
    renderPage("/incidents?status=Bogus&page=-3&sort=hack")

    await waitFor(() => expect(listIncidents).toHaveBeenCalled())

    expect(lastParams().status).toBeUndefined()
    expect(lastParams()).toMatchObject({ page: 1, sortBy: "createdAt", sortDirection: "desc" })
  })

  it("asks for the next page when Next is clicked", async () => {
    vi.mocked(listIncidents).mockResolvedValue(pageOf([makeIncident()], { totalCount: 45, totalPages: 3 }))
    renderPage("/incidents")

    fireEvent.click(await screen.findByRole("button", { name: "Next" }))

    await waitFor(() => expect(lastParams().page).toBe(2))
  })

  it("says there are no incidents yet when the list is empty and unfiltered", async () => {
    renderPage("/incidents")

    expect(await screen.findByText("No incidents yet.")).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Clear filters" })).toBeNull()
  })

  it("explains an empty result caused by filters, and clears them on request", async () => {
    renderPage("/incidents?status=Open")

    expect(await screen.findByText(/No incidents match your filters/)).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }))

    await waitFor(() => expect(lastParams().status).toBeUndefined())
  })

  it("shows the error and recovers when Try again is clicked", async () => {
    vi.mocked(listIncidents)
      .mockRejectedValueOnce(new ApiError({ status: 500, title: "Server exploded" }))
      .mockResolvedValue(pageOf([makeIncident()]))
    renderPage("/incidents")

    expect(await screen.findByText("Server exploded")).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Try again" }))

    expect(await screen.findByRole("link", { name: "Database is down" })).toBeTruthy()
  })

  it.each(["Admin", "Responder"] as const)("offers New incident to a %s", async (role) => {
    renderPage("/incidents", role)

    expect(await screen.findByRole("link", { name: "New incident" })).toBeTruthy()
  })

  it("does not offer New incident to a Viewer", async () => {
    vi.mocked(listIncidents).mockResolvedValue(pageOf([makeIncident()]))
    renderPage("/incidents", "Viewer")

    await screen.findByRole("link", { name: "Database is down" })

    expect(screen.queryByRole("link", { name: "New incident" })).toBeNull()
  })
})