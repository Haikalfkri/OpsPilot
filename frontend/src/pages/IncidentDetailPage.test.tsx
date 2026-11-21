import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react"
import { createMemoryRouter, RouterProvider } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/errors"
import {
  addComment,
  assignIncident,
  changeIncidentStatus,
  getIncident,
  listActivities,
  listComments,
  updateIncident,
} from "@/api/incidents"
import { listTeamMembers } from "@/api/team"
import { AuthContext, type AuthContextValue } from "@/hooks/auth-context"
import IncidentDetailPage from "@/pages/IncidentDetailPage"
import type { PagedResult } from "@/types/api"
import type { UserRole } from "@/types/auth"
import type { IncidentActivity, IncidentComment, IncidentDetail } from "@/types/incident"
import type { TeamMember } from "@/types/team"

// Semua pemanggil API diganti fungsi palsu: tes tidak menyentuh jaringan
vi.mock("@/api/incidents", () => ({
  getIncident: vi.fn(),
  updateIncident: vi.fn(),
  changeIncidentStatus: vi.fn(),
  assignIncident: vi.fn(),
  listActivities: vi.fn(),
  listComments: vi.fn(),
  addComment: vi.fn(),
}))
vi.mock("@/api/team", () => ({ listTeamMembers: vi.fn() }))

const ID = "11111111-1111-1111-1111-111111111111"
const RESPONDER_ID = "a0000000-0000-0000-0000-000000000002"
const VIEWER_ID = "a0000000-0000-0000-0000-000000000003"

function makeDetail(overrides: Partial<IncidentDetail> = {}): IncidentDetail {
  return {
    id: ID,
    teamId: "team-1",
    title: "Database is down",
    description: "Cannot connect to the primary database.",
    severity: "Critical",
    status: "Open",
    createdById: "user-9",
    createdByName: "Demo Admin",
    assigneeId: null,
    assigneeName: null,
    createdAtUtc: "2026-10-30T08:00:00Z",
    updatedAtUtc: "2026-10-30T08:00:00Z",
    firstRespondedAtUtc: null,
    resolvedAtUtc: null,
    closedAtUtc: null,
    responseDueAtUtc: "2026-10-30T08:15:00Z",
    resolutionDueAtUtc: "2026-10-30T12:00:00Z",
    responseSla: "Pending",
    resolutionSla: "Pending",
    allowedNextStatuses: ["Investigating", "Resolved"],
    ...overrides,
  }
}

function pageOf<T>(items: T[]): PagedResult<T> {
  return { items, page: 1, pageSize: 50, totalCount: items.length, totalPages: 1 }
}

const activity = (overrides: Partial<IncidentActivity>): IncidentActivity => ({
  id: crypto.randomUUID(),
  type: "Created",
  message: "Incident created",
  actorId: "user-9",
  actorName: "Demo Admin",
  createdAtUtc: "2026-10-30T08:00:00Z",
  ...overrides,
})

const members: TeamMember[] = [
  { id: "a0000000-0000-0000-0000-000000000001", displayName: "Demo Admin", role: "Admin" },
  { id: RESPONDER_ID, displayName: "Demo Responder", role: "Responder" },
  { id: VIEWER_ID, displayName: "Demo Viewer", role: "Viewer" },
]

function renderPage(role: UserRole = "Admin") {
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
  const router = createMemoryRouter([{ path: "/incidents/:id", element: <IncidentDetailPage /> }], {
    initialEntries: [`/incidents/${ID}`],
  })

  render(
    <QueryClientProvider client={client}>
      <AuthContext.Provider value={auth}>
        <RouterProvider router={router} />
      </AuthContext.Provider>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(getIncident).mockResolvedValue(makeDetail())
  vi.mocked(listActivities).mockResolvedValue(pageOf([activity({})]))
  vi.mocked(listComments).mockResolvedValue(pageOf<IncidentComment>([]))
  vi.mocked(listTeamMembers).mockResolvedValue(pageOf(members))
})

describe("IncidentDetailPage: reading", () => {
  it("shows the incident, its creator, and the timeline", async () => {
    vi.mocked(listActivities).mockResolvedValue(
      pageOf([
        activity({ message: "Incident created" }),
        activity({ type: "ResponseSlaBreached", message: "SLA response target breached", actorId: null, actorName: null }),
      ]),
    )
    renderPage()

    expect(await screen.findByText("Database is down")).toBeTruthy()
    expect(screen.getByText("Cannot connect to the primary database.")).toBeTruthy()
    expect(screen.getByText("Demo Admin")).toBeTruthy()
    expect(await screen.findByText("SLA response target breached")).toBeTruthy()
    expect(screen.getByText(/^System ·/)).toBeTruthy() // aksi tanpa pelaku ditampilkan sebagai System
  })

  it("shows existing comments", async () => {
    vi.mocked(listComments).mockResolvedValue(
      pageOf<IncidentComment>([
        {
          id: "c1",
          incidentId: ID,
          authorId: "u1",
          authorName: "Demo Responder",
          body: "Looking into it",
          createdAtUtc: "2026-10-30T08:05:00Z",
        },
      ]),
    )
    renderPage()

    expect(await screen.findByText("Looking into it")).toBeTruthy()
    expect(screen.getByText("Demo Responder")).toBeTruthy()
  })

  it("explains when the incident does not exist", async () => {
    vi.mocked(getIncident).mockRejectedValue(new ApiError({ status: 404, title: "Not found" }))
    renderPage()

    expect(await screen.findByText("Incident not found")).toBeTruthy()
  })
})

describe("IncidentDetailPage: status", () => {
  it("offers exactly the actions the backend allows", async () => {
    renderPage()

    expect(await screen.findByRole("button", { name: "Start investigating" })).toBeTruthy()
    expect(screen.getByRole("button", { name: "Mark resolved" })).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Close incident" })).toBeNull()
  })

  it("starts investigating without asking for confirmation, and shows the new state", async () => {
    vi.mocked(changeIncidentStatus).mockResolvedValue(
      makeDetail({ status: "Investigating", allowedNextStatuses: ["Resolved"] }),
    )
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Start investigating" }))

    await waitFor(() => expect(changeIncidentStatus).toHaveBeenCalledWith(ID, "Investigating"))
    await waitFor(() => expect(screen.queryByRole("button", { name: "Start investigating" })).toBeNull())
    expect(screen.getByRole("button", { name: "Mark resolved" })).toBeTruthy()
  })

  it("asks for confirmation before resolving, and does nothing when cancelled", async () => {
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Mark resolved" }))

    expect(await screen.findByText(/After this it can only be closed/)).toBeTruthy()
    expect(changeIncidentStatus).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole("button", { name: "Cancel" }))

    expect(screen.queryByText(/After this it can only be closed/)).toBeNull()
    expect(changeIncidentStatus).not.toHaveBeenCalled()
  })

  it("resolves after the confirmation", async () => {
    vi.mocked(changeIncidentStatus).mockResolvedValue(
      makeDetail({ status: "Resolved", allowedNextStatuses: ["Closed"] }),
    )
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Mark resolved" }))
    fireEvent.click(await screen.findByRole("button", { name: "Confirm" }))

    await waitFor(() => expect(changeIncidentStatus).toHaveBeenCalledWith(ID, "Resolved"))
    expect(await screen.findByRole("button", { name: "Close incident" })).toBeTruthy()
  })

  it("asks for confirmation before closing", async () => {
    vi.mocked(getIncident).mockResolvedValue(makeDetail({ status: "Resolved", allowedNextStatuses: ["Closed"] }))
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Close incident" }))

    expect(await screen.findByText(/can no longer be changed/)).toBeTruthy()
    expect(changeIncidentStatus).not.toHaveBeenCalled()
  })

  it("shows the backend's message when a change is refused, and reloads the incident", async () => {
    vi.mocked(changeIncidentStatus).mockRejectedValue(
      new ApiError({ status: 409, title: "Conflict", detail: "Cannot change status from Closed to Investigating." }),
    )
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Start investigating" }))

    expect(await screen.findByText("Cannot change status from Closed to Investigating.")).toBeTruthy()
    await waitFor(() => expect(getIncident).toHaveBeenCalledTimes(2)) // diambil ulang supaya tombol sesuai
  })

  it("offers no actions, no edit, and no assignee change on a closed incident, but still allows comments", async () => {
    vi.mocked(getIncident).mockResolvedValue(makeDetail({ status: "Closed", allowedNextStatuses: [] }))
    renderPage()

    expect(await screen.findByText(/closed and can no longer be changed/)).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Edit" })).toBeNull()
    expect(screen.queryByLabelText("Assignee")).toBeNull()
    expect(screen.getByRole("button", { name: "Post comment" })).toBeTruthy()
  })
})

describe("IncidentDetailPage: assignee", () => {
  it("assigns the chosen member, and never offers a Viewer", async () => {
    vi.mocked(assignIncident).mockResolvedValue(makeDetail({ assigneeId: RESPONDER_ID, assigneeName: "Demo Responder" }))
    renderPage()

    const select = (await screen.findByLabelText("Assignee")) as HTMLSelectElement
    await screen.findByRole("option", { name: "Demo Responder" })

    expect(within(select).queryByRole("option", { name: "Demo Viewer" })).toBeNull()

    fireEvent.change(select, { target: { value: RESPONDER_ID } })

    await waitFor(() => expect(assignIncident).toHaveBeenCalledWith(ID, RESPONDER_ID))
  })

  it("removes the assignee when Unassigned is chosen", async () => {
    vi.mocked(getIncident).mockResolvedValue(makeDetail({ assigneeId: RESPONDER_ID, assigneeName: "Demo Responder" }))
    vi.mocked(assignIncident).mockResolvedValue(makeDetail())
    renderPage()

    const select = await screen.findByLabelText("Assignee")
    await screen.findByRole("option", { name: "Demo Responder" })

    fireEvent.change(select, { target: { value: "" } })

    await waitFor(() => expect(assignIncident).toHaveBeenCalledWith(ID, null))
  })
})

describe("IncidentDetailPage: comments", () => {
  it("posts the trimmed comment", async () => {
    vi.mocked(addComment).mockResolvedValue({
      id: "c2",
      incidentId: ID,
      authorId: "u1",
      authorName: "Demo Admin",
      body: "Checked the logs",
      createdAtUtc: "2026-10-30T08:10:00Z",
    })
    renderPage()

    fireEvent.change(await screen.findByLabelText("Add a comment"), { target: { value: "  Checked the logs  " } })
    fireEvent.click(screen.getByRole("button", { name: "Post comment" }))

    await waitFor(() => expect(addComment).toHaveBeenCalledWith(ID, "Checked the logs"))
  })

  it("rejects an empty comment without calling the API", async () => {
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Post comment" }))

    expect(await screen.findByText("Comment is required.")).toBeTruthy()
    expect(addComment).not.toHaveBeenCalled()
  })
})

describe("IncidentDetailPage: editing", () => {
  it("blocks a title that is too short, then saves a valid one", async () => {
    vi.mocked(updateIncident).mockResolvedValue(makeDetail({ title: "New title for the incident" }))
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Edit" }))

    const title = await screen.findByLabelText("Title")
    fireEvent.change(title, { target: { value: "abc" } })
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }))

    expect(await screen.findByText("Title must be at least 5 characters.")).toBeTruthy()
    expect(updateIncident).not.toHaveBeenCalled()

    fireEvent.change(title, { target: { value: "New title for the incident" } })
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }))

    await waitFor(() =>
      expect(updateIncident).toHaveBeenCalledWith(ID, {
        title: "New title for the incident",
        description: "Cannot connect to the primary database.",
        severity: "Critical",
      }),
    )
    expect(await screen.findByText("New title for the incident")).toBeTruthy()
    expect(screen.queryByLabelText("Title")).toBeNull() // form menutup setelah berhasil
  })

  it("keeps Save disabled until something changes", async () => {
    renderPage()

    fireEvent.click(await screen.findByRole("button", { name: "Edit" }))

    const save = (await screen.findByRole("button", { name: "Save changes" })) as HTMLButtonElement
    expect(save.disabled).toBe(true)
  })
})

describe("IncidentDetailPage: a Viewer", () => {
  it("can read, but sees no way to change anything", async () => {
    renderPage("Viewer")

    expect(await screen.findByText("Database is down")).toBeTruthy()

    expect(screen.queryByRole("button", { name: "Start investigating" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Mark resolved" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Edit" })).toBeNull()
    expect(screen.queryByLabelText("Assignee")).toBeNull()
    expect(screen.queryByRole("button", { name: "Post comment" })).toBeNull()
    expect(screen.getByText(/Viewers can read comments but not write them/)).toBeTruthy()
  })
})